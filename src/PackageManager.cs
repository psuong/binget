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

namespace BinGet;

public class PackageManager {
    private const string MozillaAgent = "Mozilla/5.0";
    private const int BufferSize = 8192;
    private const int MaxDownloads = 4;
    private readonly ILogger<PackageManager> logger;

    public PackageManager(ILogger<PackageManager> logger) {
        this.logger = logger;
    }

    private bool TryGetPackageInfo(JsonDocument jsonDoc, string pattern, string packageName, out PackageInfo packageInfo) {
        foreach (var asset in jsonDoc.RootElement.GetProperty("assets").EnumerateArray()) {
            var name = asset.GetProperty("name").GetString();
            var regex = new Regex(@$"{pattern}");

            if (regex.IsMatch(name)) {
                logger.ZLogInformation($"Found asset {name}");

                asset.GetProperty("digest");
                packageInfo = new PackageInfo(
                    name,
                    asset.GetProperty("browser_download_url").GetString());
                return true;
            }
        }
        logger.ZLogError($"Failed to find an archive to download and extract for {packageName}");
        packageInfo = default;
        return false;
    }

    private async Task ExtractArchive(string destination, string packageName, string zipPath) {
        try {
            var extractionPath = Path.Combine(destination, packageName);
            await ZipFile.ExtractToDirectoryAsync(zipPath, extractionPath, true);
            logger.ZLogInformation($"Finished extracting to: {extractionPath}");
            // Need to write a manifest file
        } catch (Exception err) {
            logger.ZLogError(err, $"Failed to extract {zipPath}.\n");
        } finally {
            File.Delete(zipPath);
            logger.ZLogInformation($"Removed temporary archive: {zipPath}");
        }
    }

    // TODO: Grab the manifest first and check to see if I need to grab a new version, add a flag to override this from the CLI
    private async Task DownloadAndExtractPackage(DownloadArgs downloadArgs) {
        var (httpClient, config, repositoryConfig, packageName, sem) = downloadArgs;
        var url = $"{config.Url}{repositoryConfig.Id}/releases/latest";
        logger.ZLogInformation($"Downloading from: {url}");

        try {
            await sem.WaitAsync();

            var response = await httpClient.GetStringAsync(url);
            using var jsonDoc = JsonDocument.Parse(response);

            if (!TryGetPackageInfo(
                    jsonDoc,
                    repositoryConfig.TargetPattern,
                    packageName,
                    out var packageInfo)) {
                logger.ZLogCritical($"Failed to find asset for {packageName}");
                return;
            }

            JsonElement digestElement = jsonDoc.RootElement.GetProperty("digest");
            logger.ZLogInformation($"Digest: {digestElement.GetString()}");

            using var downloadResponse = await httpClient.GetAsync(
                packageInfo.DownloadUrl,
                HttpCompletionOption.ResponseHeadersRead);
            downloadResponse.EnsureSuccessStatusCode();

            var zipPath = Path.Combine(config.Destination, packageInfo.FileName);
            await using (var input = await downloadResponse.Content.ReadAsStreamAsync()) {
                await using (var output = File.Create(zipPath)) {
                    using var bufferScope = new ArrayPoolScope<byte>(BufferSize);

                    int bytesRead = 0;
                    while ((bytesRead = await input.ReadAsync(bufferScope.AsMemory())) > 0) {
                        await output.WriteAsync(bufferScope.AsMemory()[..bytesRead]);
                    }
                }
            }

            await ExtractArchive(config.Destination, packageName, zipPath);
        } finally {
            sem.Release();
        }
    }

    [Command("install")]
    public async Task Install(string config) {
        var toml = await BinGetConfig.Load(config);
        if (!Directory.Exists(toml.Destination)) {
            logger.ZLogInformation($"Creating the directory at path {toml.Destination}");
            Directory.CreateDirectory(toml.Destination);
        }

        using var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(MozillaAgent);
        var tasks = new List<Task>(toml.Repositories.Count);
        var sem = new SemaphoreSlim(MaxDownloads, MaxDownloads);

        var it = toml.Repositories.GetEnumerator();
        while (it.MoveNext()) {
            (var packageName, var repositoryConfig) = it.Current;
            var packagePath = Path.Join(toml.Destination, packageName);
            tasks.Add(DownloadAndExtractPackage(new DownloadArgs(httpClient, toml, repositoryConfig, packageName, sem)));
        }

        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// Removes any local packages that are not listed in the config file. Your config file is effectively your primary list.
    /// </summary>
    /// <param name="config">The configuration toml file that lists the packages.</param>
    /// <param name="updatePath">Updates the path variables.</param>
    [Command("clean")]
    public async Task Clean(string config, bool updatePath) {
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

        var remainingIt = packagePaths.GetEnumerator();
        while (remainingIt.MoveNext()) {
            logger.ZLogInformation($"Removing package at: {remainingIt.Current}");
            Directory.Delete(remainingIt.Current);
            // TODO: Update the path variables
        }
    }
}
