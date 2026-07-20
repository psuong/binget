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
using System.Threading;
using System;

namespace BinGet;

public class PackageManager {
    private const string MozillaAgent = "Mozilla/5.0";
    private const int BufferSize = 8192;

    private readonly ILogger<PackageManager> logger;

    public PackageManager(ILogger<PackageManager> logger) {
        this.logger = logger;
    }

    private bool TryGetPackageUrl(JsonDocument jsonDoc, string pattern, string packageName, out string fileName, out string downloadUrl) {
        foreach (var asset in jsonDoc.RootElement.GetProperty("assets").EnumerateArray()) {
            var name = asset.GetProperty("name").GetString();
            var regex = new Regex(@$"{pattern}");

            if (regex.IsMatch(name)) {
                logger.ZLogInformation($"Found asset {name}");
                fileName = name;
                downloadUrl = asset.GetProperty("browser_download_url").GetString();
                return true;
            }
        }
        logger.ZLogError($"Failed to find an archive to download and extract for {packageName}");
        fileName = string.Empty;
        downloadUrl = string.Empty;
        return false;
    }

    private async Task DownloadPackage(HttpClient httpClient, BinGetConfig config, string packageName, RepositoryConfig repositoryConfig) {
        var url = $"{config.Url}{repositoryConfig.Id}/releases/latest";
        logger.ZLogInformation($"Downloading from: {url}");

        var response = await httpClient.GetStringAsync(url);
        using var jsonDoc = JsonDocument.Parse(response);

        if (!TryGetPackageUrl(
                jsonDoc,
                repositoryConfig.TargetPattern,
                packageName,
                out var fileName,
                out var downloadUrl)) {
            logger.ZLogCritical($"Failed to find asset for {packageName}");
            return;
        }

        using var downloadResponse = await httpClient.GetAsync(
            downloadUrl,
            HttpCompletionOption.ResponseHeadersRead);
        downloadResponse.EnsureSuccessStatusCode();

        var zipPath = Path.Combine(config.Destination, fileName);
        await using var input = await downloadResponse.Content.ReadAsStreamAsync();
        await using var output = File.Create(zipPath);
        using var bufferScope = new ArrayPoolScope<byte>(BufferSize);

        int bytesRead = 0;
        while ((bytesRead = await input.ReadAsync(bufferScope.AsMemory())) > 0) {
            await output.WriteAsync(bufferScope.AsMemory()[..bytesRead]);
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

        var it = toml.Repositories.GetEnumerator();
        while (it.MoveNext()) {
            (var packageName, var repositoryConfig) = it.Current;
            var packagePath = Path.Join(toml.Destination, packageName);
            tasks.Add(DownloadPackage(httpClient, toml, packageName, repositoryConfig));
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
