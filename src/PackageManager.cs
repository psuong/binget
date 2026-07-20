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

    [Command("install")]
    public async Task Install(string config) {
        var toml = await BinGetConfig.Load(config);
        if (!Directory.Exists(toml.Destination)) {
            logger.ZLogInformation($"Creating the directory at path {toml.Destination}");
            Directory.CreateDirectory(toml.Destination);
        }

        using var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(MozillaAgent);

        var ct = new CancellationToken();

        var it = toml.Repositories.GetEnumerator();
        while (it.MoveNext()) {
            (var packageName, var repositoryConfig) = it.Current;
            var packagePath = Path.Join(toml.Destination, packageName);
            var url = $"{toml.Url}{repositoryConfig.Id}/releases/latest";

            var response = await httpClient.GetStringAsync(url);
            using var jsonDoc = JsonDocument.Parse(response);
            logger.ZLogInformation($"Downloading from: {url}");

            if (TryGetPackageUrl(
                jsonDoc,
                it.Current.Value.TargetPattern,
                packageName,
                out var fileName,
                out var downloadUrl)) {

                using var downloadResponse = await httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseContentRead);
                if (downloadResponse.EnsureSuccessStatusCode().IsSuccessStatusCode) {
                    var totalBytes = downloadResponse.Content.Headers.ContentLength ?? -1;
                    var bytesRead = 0;
                    long totalRead = 0;

                    using var contentStream = await downloadResponse.Content.ReadAsStreamAsync();
                    using var bufferScope = new ArrayPoolScope(BufferSize);
                    var zipPath = Path.Combine(toml.Destination, fileName);

                    using var fileStream = new FileStream(zipPath, FileMode.Create, FileAccess.Write, FileShare.None, BufferSize, true);
                    while ((bytesRead = await contentStream.ReadAsync(bufferScope.AsMemory(), ct)) > 0) {
                        await fileStream.WriteAsync(bufferScope.AsReadonlyMemory(), ct);
                        totalRead += bytesRead;
                    }
                }
            } else {
                logger.ZLogCritical($"Failed to download the {fileName} from {downloadUrl}");
            }
        }
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
