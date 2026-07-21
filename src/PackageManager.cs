using ConsoleAppFramework;
using ZLogger;
using Microsoft.Extensions.Logging;
using BinGet.Data;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System;
using System.IO.Compression;
using System.Threading;
using System.Reflection;
using Scriban;
using Cysharp.IO;
using System.Text;
using Tomlyn;
using BinGet.Utils;
using Spectre.Console;
using BinGet.Logging;

namespace BinGet;

public class PackageManager {
    private const string MozillaAgent = "Mozilla/5.0";
    private const int BufferSize = 8192;
    private const int MaxDownloads = 4;
    private readonly ILogger<PackageManager> logger;
    private readonly Template manifestTemplate;
    private readonly List<ProgressTask> progressTasks;
    private readonly List<(string pkgName, PackageStatus pkgStatus)> summary;

    public PackageManager(ILogger<PackageManager> logger) {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("BinGet.templates.manifest.scriban");
        using var reader = new StreamReader(stream);
        manifestTemplate = Template.Parse(reader.ReadToEnd());
        progressTasks = new List<ProgressTask>(MaxDownloads);
        summary = new List<(string, PackageStatus)>(MaxDownloads);
        this.logger = logger;
    }

    /// <summary>
    /// Atempts to get the package info from a json response.
    /// </summary>
    /// <param name="jsonDoc">The json response object.</param>
    /// <param name="pattern">The regex pattern.</param>
    /// <param name="packageName">The name of the package.</param>
    /// <param name="packageInfo">A compact object with all of the package info.</param>
    /// <returns>True, if the information is found.</returns>
    private bool TryGetPackageInfo(JsonDocument jsonDoc, string pattern, string packageName, out PackageInfo packageInfo) {
        foreach (var asset in jsonDoc.RootElement.GetProperty("assets").EnumerateArray()) {
            var name = asset.GetProperty("name").GetString();
            var regex = new Regex(@$"{pattern}");

            if (regex.IsMatch(name)) {
                logger.ZLogInformation($"Found asset {name}");

                packageInfo = new PackageInfo(
                    name,
                    asset.GetProperty("browser_download_url").GetString(),
                    asset.GetProperty("digest").GetString(),
                    jsonDoc.RootElement.GetProperty("tag_name").GetString());
                logger.ZLogInformation($"Package Info: {packageInfo}");
                return true;
            }
        }
        logger.ZLogError($"Failed to find an archive to download and extract for {packageName}");
        packageInfo = default;
        return false;
    }

    /// <summary>
    /// Checks if the package downloaded should be updated if the version downloaded is more recent than the current version.
    /// </summary>
    /// <param name="destination">The directory where the package should be downloaded.</param>
    /// <param name="packageName">The name of the package.</param>
    /// <param name="newVersion">The new version of the package.</param>
    /// <returns>True if newVersion > existingVersion.</returns>
    private async ValueTask<bool> CanDownloadAsset(string destination, string packageName, string newVersion) {
        var manifestPath = Path.Join(destination, packageName, "manifest.toml");
        if (File.Exists(manifestPath)) {
            using var streamReader = new Utf8StreamReader(manifestPath, FileOpenMode.Throughput);
            var text = Encoding.UTF8.GetString(await streamReader.ReadToEndAsync());
            var manifest = TomlSerializer.Deserialize<ManifestConfig>(text, TomlManifestConfigContext.Default);

            // Get the version
            if (VersionHelper.TryParseVersion(manifest.Tag, out var existing) &&
                VersionHelper.TryParseVersion(newVersion, out var downloaded)) {

                if (downloaded > existing) {
                    return true;
                } else {
                    logger.ZLogInformation($"Aborted download of {packageName}, the existing version: {existing} is more recent than the downloaded version: {downloaded}");
                    return false;
                }
            }
            logger.ZLogInformation($"Failed to parse the versions for {packageName}");
            return false;
        }
        // We did not find a manifest.toml in the package which means that we probably did not download the asset.
        return true;
    }

    /// <summary>
    /// Attemps to extract the archive to a destination directory. Generates a manifest after extraction.
    /// </summary>
    /// <param name="manifestArgs">Manifest info to be written to after extraction.</param>
    /// <returns>An awaitable task that represents a handle until the operations are done.</returns>
    private async ValueTask<bool> ExtractArchiveAndGenerateManifest(ManifestArgs manifestArgs) {
        var status = true;
        try {
            // First check if the downloaded archive has the same Checksum hash
            using (var fs = File.OpenRead(manifestArgs.ZipPath)) {
                if (!await HashUtils.CompareChecksum(manifestArgs.Checksum, fs, logger)) {
                    return false;
                }
            }

            var extractionPath = Path.Join(manifestArgs.Destination, manifestArgs.PackageName);
            await ZipFile.ExtractToDirectoryAsync(manifestArgs.ZipPath, extractionPath, true);
            logger.ZLogInformation($"Finished extracting to: {extractionPath}");
            // Need to write/overwrite a manifest file
            var manifestPath = Path.Join(manifestArgs.Destination, manifestArgs.PackageName, "manifest.toml");
            await File.WriteAllTextAsync(
                manifestPath,
                await manifestTemplate.RenderAsync(new TemplateContext(manifestArgs.ToScriptObject())));
        } catch (Exception err) {
            logger.ZLogError(err, $"Failed to extract {manifestArgs.ZipPath}.\n");
            status = false;
        } finally {
            File.Delete(manifestArgs.ZipPath);
            logger.ZLogInformation($"Removed temporary archive: {manifestArgs.ZipPath}");
        }
        return status;
    }

    /// <summary>
    /// Atempts to download the package and generate the package. This is the primary method of the package manager.
    /// </summary>
    /// <param name="downloadArgs">Metadata describing where to get the package.</param>
    /// <returns>An awaitable Task representing when the operation is done.</returns>
    private async Task DownloadAndExtractPackage(DownloadArgs downloadArgs) {
        var (httpClient, config, repositoryConfig, packageName, sem, taskId) = downloadArgs;
        var url = $"{config.Url}{repositoryConfig.Id}/releases/latest";
        logger.ZLogInformation($"Downloading from: {url}");
        var pkgStatus = PackageStatus.NotStarted;

        try {
            // We only have a maximum # of downloads to not spam the REST endpoints.
            await sem.WaitAsync();

            var response = await httpClient.GetStringAsync(url);
            using var jsonDoc = JsonDocument.Parse(response);

            if (!TryGetPackageInfo(
                    jsonDoc,
                    repositoryConfig.TargetPattern,
                    packageName,
                    out var packageInfo)) {
                logger.ZLogCritical($"Failed to find asset for {packageName}");
                pkgStatus = PackageStatus.Failed;
                return;
            }

            // Reset the progress
            var progress = progressTasks[taskId % MaxDownloads];
            progress.Value = 0;
            progress.Description = $"[yellow]↓ {packageName}[/]";
            progress.StartTask();

            if (!await CanDownloadAsset(config.Destination, packageName, packageInfo.Tag)) {
                progress.Description = $"[grey]Skipped {packageName}[/]";
                progress.Value = 100;
                pkgStatus = PackageStatus.Skipped;
                return;
            }

            using var downloadResponse = await httpClient.GetAsync(
                packageInfo.DownloadUrl,
                HttpCompletionOption.ResponseHeadersRead);
            downloadResponse.EnsureSuccessStatusCode();

            var zipPath = Path.Combine(config.Destination, packageInfo.FileName);
            await using (var input = await downloadResponse.Content.ReadAsStreamAsync()) {
                await using var output = File.Create(zipPath);
                using var bufferScope = new ArrayPoolScope<byte>(BufferSize);
                var totalBytes = downloadResponse.Content.Headers.ContentLength ?? -1;
                var bytesRead = 0;
                var totalRead = 0;
                while ((bytesRead = await input.ReadAsync(bufferScope.AsMemory())) > 0) {
                    await output.WriteAsync(bufferScope.AsMemory()[..bytesRead]);
                    totalRead += bytesRead;
                    progress.Value = ((double)totalRead) / totalBytes * 100.0;
                }
            }

            var status = await ExtractArchiveAndGenerateManifest(new ManifestArgs(
                config.Destination,
                packageName,
                zipPath,
                repositoryConfig.Id,
                packageInfo.FileName,
                packageInfo.Checksum,
                packageInfo.Tag));
            pkgStatus = status ? PackageStatus.Installed : PackageStatus.Failed;
            progress.Description = status ? $"[green]✓ {packageName}[/]" : $"[red]✗ {repositoryConfig.Id}[/]";
        } finally {
            sem.Release();
            summary.Add((packageName, pkgStatus));
        }
    }

    /// <summary>
    /// Installs/updates packages from a configuration file.
    /// </summary>
    /// <param name="config">The primary configuration file.</param>
    [Command("install|update|i|u")]
    public async Task Install(string config) {
        var toml = await BinGetConfig.Load(config);
        if (!Directory.Exists(toml.Destination)) {
            logger.ZLogInformation($"Creating the directory at path {toml.Destination}");
            Directory.CreateDirectory(toml.Destination);
        }

        await AnsiConsole.Progress()
            .AutoRefresh(true)
            .HideCompleted(false)
            .StartAsync(async (ctx) => {
                using var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(MozillaAgent);
                var tasks = new List<Task>(toml.Repositories.Count);
                var sem = new SemaphoreSlim(MaxDownloads, MaxDownloads);

                // If there are less than the MaxDownloads then I don't need to spawn the MaxDownload ProgressBars
                for (var i = 0; i < Math.Min(toml.Repositories.Count, MaxDownloads); i++) {
                    progressTasks.Add(ctx.AddTask("Package Install", false, 100));
                }

                var it = toml.Repositories.GetEnumerator();
                var count = 0;
                while (it.MoveNext()) {
                    (var packageName, var repositoryConfig) = it.Current;
                    var packagePath = Path.Join(toml.Destination, packageName);
                    var task = DownloadAndExtractPackage(new DownloadArgs(
                        httpClient,
                        toml,
                        repositoryConfig,
                        packageName,
                        sem,
                        count));
                    tasks.Add(task);
                    count++;
                }
                await Task.WhenAll(tasks);
            });

        // Draw the summary
        var (installed, skipped, fail, unknown) = summary.CountStatus();
        AnsiConsole.WriteLine($"Installed: {installed}, Skipped: {skipped}, Failed: {fail}");
        var summaryTable = new Table()
            .AddColumn("Package")
            .AddColumn("Status");

        for (var i = 0; i < summary.Count; i++) {
            var (pkgName, pkgStatus) = summary[i];
            summaryTable.AddRow(pkgName, pkgStatus.Format());
        }
        AnsiConsole.Write(summaryTable);

        if (fail > 0) {
            AnsiConsole.WriteLine($"Check the logfile for any errors. The log file is located at: {LogUtils.GetLogPath()}");
        }
    }

    /// <summary>
    /// Removes any local packages that are not listed in the config file. Your config file is effectively your primary list.
    /// </summary>
    /// <param name="config">The configuration toml file that lists the packages.</param>
    [Command("clean|c")]
    public async Task Clean(string config) {
        var toml = await BinGetConfig.Load(config);
        if (!Directory.Exists(toml.Destination)) {
            logger.ZLogInformation($"Creating the directory at path {toml.Destination}");
            Directory.CreateDirectory(toml.Destination);
        }

        HashSet<string> packagePaths = [.. Directory.GetDirectories(toml.Destination)];
        var it = toml.Repositories.GetEnumerator();
        while (it.MoveNext()) {
            (var packageName, var _) = it.Current;
            var packagePath = Path.Join(toml.Destination, packageName);
            packagePaths.Remove(packagePath);
        }

        if (packagePaths.Count > 0) {
            var removedTable = new Table().AddColumn("Removed Package Paths");
            var remainingIt = packagePaths.GetEnumerator();
            while (remainingIt.MoveNext()) {
                logger.ZLogInformation($"Removing package at: {remainingIt.Current}");
                removedTable.AddRow(remainingIt.Current);
                Directory.Delete(remainingIt.Current, true);
            }
            AnsiConsole.Write(removedTable);
        } else {
            AnsiConsole.WriteLine("No packages to remove.");
        }
    }

    /// <summary>
    /// Displays all packages in the destination directory and the config file.
    /// </summary>
    /// <param name="config">The path to the configuration file.</param>
    [Command("list|l")]
    public async Task ListPackages(string config) {
        var summary = new Table().AddColumns("Package", "Status");
        var toml = await BinGetConfig.Load(config);

        var subDirectories = Directory.GetDirectories(toml.Destination);
        var installedPkgs = new HashSet<string>(subDirectories);

        var it = toml.Repositories.GetEnumerator();
        while (it.MoveNext()) {
            var (packageName, _) = it.Current;
            var packagePath = Path.Join(toml.Destination, packageName);
            summary.AddRow(packageName,
                Directory.Exists(packagePath) ? "[green]✓ Installed[/]" : "[red]✗ Uninstalled[/]");
            installedPkgs.Remove(packagePath);
        }

        var installedIt = installedPkgs.GetEnumerator();
        while (installedIt.MoveNext()) {
            var pkgName = Path.GetFileName(installedIt.Current);
            summary.AddRow(pkgName, "[purple]? Unlisted in configuration file.[/]");
        }
        AnsiConsole.Write(summary);
    }
}
