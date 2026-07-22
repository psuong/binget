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
using Cysharp.IO;
using System.Text;
using Tomlyn;
using BinGet.Utils;
using Spectre.Console;
using BinGet.Logging;
using BinGet.Pool;

namespace BinGet;

public class PackageManager {
    private const string MozillaAgent = "Mozilla/5.0";
    private const int BufferSize = 8192;
    private const int MaxDownloads = 4;
    private readonly ILogger<PackageManager> logger;
    private readonly List<ProgressTask> progressTasks;
    private (string pkgName, PackageStatus pkgStatus)[] summary;

    public PackageManager(ILogger<PackageManager> logger) {
        progressTasks = new List<ProgressTask>(MaxDownloads);
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
    private PackageStatus TryGetPackageInfo(JsonDocument jsonDoc, string pattern, string packageName, out PackageInfo packageInfo) {
        var it = jsonDoc.RootElement.GetProperty("assets").EnumerateArray();
        while (it.MoveNext()) {
            var asset = it.Current;
            var name = asset.GetProperty("name").GetString();
            var regex = new Regex(@$"{pattern}");
            if (regex.IsMatch(name)) {
                packageInfo = new PackageInfo(
                    name,
                    asset.GetProperty("browser_download_url").GetString(),
                    asset.GetProperty("digest").GetString(),
                    jsonDoc.RootElement.GetProperty("tag_name").GetString());
                logger.ZLogInformation($"Package Info: {packageInfo}");
                return PackageStatus.Success;
            }
        }
        logger.ZLogError($"Failed to find an archive to download and extract for {packageName}");
        packageInfo = default;
        return PackageStatus.PackageNotFound;
    }

    /// <summary>
    /// Checks if the package downloaded should be updated if the version downloaded is more recent than the current version.
    /// </summary>
    /// <param name="destination">The directory where the package should be downloaded.</param>
    /// <param name="packageName">The name of the package.</param>
    /// <param name="newVersion">The new version of the package.</param>
    /// <returns>True if newVersion > existingVersion.</returns>
    private async ValueTask<PackageStatus> CanDownloadAsset(string destination, string packageName, string newVersion) {
        var manifestPath = Path.Join(destination, packageName, "manifest.toml");
        if (File.Exists(manifestPath)) {
            using var streamReader = new Utf8StreamReader(manifestPath, FileOpenMode.Throughput);
            var text = Encoding.UTF8.GetString(await streamReader.ReadToEndAsync());
            var manifest = TomlSerializer.Deserialize<ManifestConfig>(text, TomlManifestConfigContext.Default);

            // Get the version
            if (VersionHelper.TryParseVersion(manifest.Tag, out var existing) &&
                VersionHelper.TryParseVersion(newVersion, out var downloaded)) {

                if (downloaded > existing) {
                    return PackageStatus.Success;
                } else if (downloaded == existing) {
                    logger.ZLogInformation($"Aborted download of {packageName}, the existing version: {existing} is the same as the downloaded version: {downloaded}");
                    return PackageStatus.Skipped;
                } else {
                    logger.ZLogInformation($"Aborted download of {packageName}, the existing version: {existing} is more recent than the downloaded version: {downloaded}");
                    return PackageStatus.VersionRegression;
                }
            }
            logger.ZLogInformation($"Failed to parse the versions for {packageName}");
            return PackageStatus.UnparseableVersion;
        }
        // We did not find a manifest.toml in the package which means that we probably did not download the asset.
        return PackageStatus.Success;
    }

    /// <summary>
    /// Attemps to extract the archive to a destination directory. Generates a manifest after extraction.
    /// </summary>
    /// <param name="manifestArgs">Manifest info to be written to after extraction.</param>
    /// <returns>An awaitable task that represents a handle until the operations are done.</returns>
    private async ValueTask<PackageStatus> ExtractArchiveAndGenerateManifest(ManifestArgs manifestArgs) {
        PackageStatus status = PackageStatus.Success;
        try {
            // First check if the downloaded archive has the same Checksum hash
            using (var fs = File.OpenRead(manifestArgs.ZipPath)) {
                if (!await HashUtils.CompareChecksum(manifestArgs.Checksum, fs, logger)) {
                    return PackageStatus.ChecksumMismatch;
                }
            }

            var extractionPath = Path.Join(manifestArgs.Destination, manifestArgs.PackageName);
            await ZipFile.ExtractToDirectoryAsync(manifestArgs.ZipPath, extractionPath, true);
            logger.ZLogInformation($"Finished extracting to: {extractionPath}");
            // Need to write/overwrite a manifest file
            var manifestPath = Path.Join(manifestArgs.Destination, manifestArgs.PackageName, "manifest.toml");
            using var _ = new ObjectPoolScope<StringBuilder>(
                static () => new StringBuilder(256),
                static sb => sb.Clear(),
                out var sb);
            manifestArgs.Format(sb, manifestPath);
        } catch (Exception err) {
            status = PackageStatus.ExtractionFailure;
            logger.ZLogError(err, $"Failed to extract {manifestArgs.ZipPath}.\n");
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
    private async Task<(string name, PackageStatus status)> DownloadAndExtractPackage(DownloadArgs downloadArgs) {
        var (httpClient, config, repositoryConfig, packageName, sem, taskId) = downloadArgs;
        var url = $"{config.Url}{repositoryConfig.Id}/releases/latest";
        logger.ZLogInformation($"Downloading from: {url}");
        var (pkgName, pkgStatus) = (packageName, PackageStatus.Success);

        try {
            // We only have a maximum # of downloads to not spam the REST endpoints.
            await sem.WaitAsync();

            var response = await httpClient.GetStringAsync(url);
            using var jsonDoc = JsonDocument.Parse(response);

            if ((pkgStatus = TryGetPackageInfo(
                    jsonDoc,
                    repositoryConfig.TargetPattern,
                    packageName,
                    out var packageInfo)) != PackageStatus.Success) {
                logger.ZLogCritical($"Failed to find asset for {packageName}");
                return (pkgName, pkgStatus);
            }

            // Reset the progress
            var progress = progressTasks[taskId % MaxDownloads];
            progress.Value = 0;
            progress.Description = $"[yellow]↓ {packageName}[/]";
            progress.StartTask();

            if ((pkgStatus = await CanDownloadAsset(config.Destination, packageName, packageInfo.Tag)) != PackageStatus.Success) {
                progress.Description = $"[grey]Skipped {packageName}[/]";
                progress.Value = 100;
                return (pkgName, pkgStatus);
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

            pkgStatus = await ExtractArchiveAndGenerateManifest(new ManifestArgs(
                config.Destination,
                packageName,
                zipPath,
                repositoryConfig.Id,
                packageInfo.FileName,
                packageInfo.Checksum,
                packageInfo.Tag));
            progress.Description = pkgStatus.IsSuccess() ? $"[green]✓ {packageName}[/]" : $"[red]✗ {pkgName}[/]";
        } finally {
            sem.Release();
        }
        return (pkgName, pkgStatus);
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
                var tasks = new List<Task<(string name, PackageStatus status)>>(toml.Repositories.Count);
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
                summary = await Task.WhenAll(tasks);
            });

        var result = summary.CountStatus();
        // Draw the summary
        AnsiConsole.WriteLine($"Installed: {result.installed}, Failed: {result.failed}, Skipped: {result.skipped}");
        var summaryTable = new Table()
            .AddColumn("Package")
            .AddColumn("Status");

        for (var i = 0; i < summary.Length; i++) {
            var (pkgName, pkgStatus) = summary[i];
            summaryTable.AddRow(pkgName, pkgStatus.Format());
        }
        AnsiConsole.Write(summaryTable);

        if (result.failed > 0) {
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
            var removedTable = new Table().AddColumn("Package").AddColumn("Path Removed");
            var remainingIt = packagePaths.GetEnumerator();
            while (remainingIt.MoveNext()) {
                logger.ZLogInformation($"Removing package at: {remainingIt.Current}");
                removedTable.AddRow(Path.GetFileName(remainingIt.Current), $"[red]{remainingIt.Current}[/]");
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
