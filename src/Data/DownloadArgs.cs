using System.Collections.Generic;
using System.Net.Http;
using System.Threading;

namespace BinGet.Data;

/// <summary>
/// Metadata to help with downloading the archive.
/// </summary>
/// <param name="HttpClient">The web client responsible for making the request and fetching the data.</param>
/// <param name="BinGetConfig">The primary configuration file.</param>
/// <param name="RepositoryConfig">The configuration object of the package.</param>
/// <param name="PackageName">The name of the package.</param>
/// <param name="Semaphore">A semaphore to limit the downloads.</param>
/// <param name="TaskId">The ID of the task.</param>
public readonly record struct DownloadArgs(
    HttpClient HttpClient,
    BinGetConfig BinGetConfig,
    RepositoryConfig RepositoryConfig,
    string PackageName,
    SemaphoreSlim Semaphore,
    int TaskId);

public enum PackageStatus {
    NotStarted,
    Installed,
    Skipped,
    Failed
}

public static class PackageStatusExtensions {
    public static string Format(this PackageStatus status) {
        return status switch {
            PackageStatus.Installed => "[blue]✓ - Installed[/]",
            PackageStatus.Skipped => "[grey]>> - Skipped[/]",
            PackageStatus.Failed => "[red]✗ - Failed[/]",
            _ => "[grey]? - Unknown[/]",
        };
    }

    public static (int installed, int skipped, int failed, int unknown) CountStatus<T>(
        this IReadOnlyList<(T, PackageStatus)> collection) {
        var installed = 0;
        var skipped = 0;
        var failed = 0;
        var unknown = 0;
        for (var i = 0; i < collection.Count; i++) {
            (var _, var status) = collection[i];
            switch (status) {
                case PackageStatus.NotStarted:
                    unknown++;
                    break;
                case PackageStatus.Installed:
                    installed++;
                    break;
                case PackageStatus.Skipped:
                    skipped++;
                    break;
                case PackageStatus.Failed:
                    failed++;
                    break;
                default:
                    break;
            }
        }
        return (installed, skipped, failed, unknown);
    }
}
