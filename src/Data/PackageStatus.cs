using System.Collections.Generic;

namespace BinGet.Data;

/// <summary>
/// The return status to allow for more descriptive failures.
/// </summary>
public enum PackageStatus {
    /// <summary>
    /// Everything succeeded.
    /// </summary>
    Success = 1,
    /// <summary>
    /// The package downloaded was corrupted so the checksum was not correct.
    /// </summary>
    ChecksumMismatch,
    /// <summary>
    /// The version # was not parseable. The package must support semantic versioning.
    /// </summary>
    UnparseableVersion,
    /// <summary>
    /// The version downloaded was older than the version already installed.
    /// </summary>
    VersionRegression,
    /// <summary>
    /// The package was not found on the repository.
    /// </summary>
    PackageNotFound,
    /// <summary>
    /// An error occured when extracting the package archive.
    /// </summary>
    ExtractionFailure,
    /// <summary>
    /// The version was skipped because it is already installed.
    /// </summary>
    VersionSkipped
}

public static class PackageStatusExtensions {
    /// <summary>
    /// Formats the <see cref="PackageStatus"/> to a friendly readable format in the terminal.
    /// </summary>
    /// <param name="status">The current state of the pkg manager.</param>
    /// <returns>string</returns>
    public static string Format(this PackageStatus status) {
        return status switch {
            PackageStatus.Success => "[blue]✓ - Installed[/]",
            PackageStatus.VersionSkipped => "[purple]>> - Skipped, Version Equivalent[/]",
            PackageStatus.ChecksumMismatch => "[red]✗ - Checksum Mismatch[/]",
            PackageStatus.UnparseableVersion => "[red]✗ - Version Number Corrupted[/]",
            PackageStatus.VersionRegression => "[red]✗ - Version Regression[/]",
            PackageStatus.PackageNotFound => "[red]✗ - Package Not Found[/]",
            PackageStatus.ExtractionFailure => "[red]✗ - Package Extraction Failure[/]",
            _ => "[grey]? - Unknown[/]",
        };
    }

    /// <summary>
    /// Counts all of the packages' status.
    /// </summary>
    /// <typeparam name="T">Any type.</typeparam>
    /// <param name="collection">The results of all packages.</param>
    /// <returns>A tuple of installed, failed, or skipped package counts.</returns>
    public static (int installed, int failed, int skipped) CountStatus<T>(
        this IReadOnlyList<(T, PackageStatus)> collection) {
        var installed = 0;
        var failed = 0;
        int skipped = 0;
        for (var i = 0; i < collection.Count; i++) {
            (var _, var status) = collection[i];
            switch (status) {
                case PackageStatus.VersionSkipped:
                    skipped++;
                    break;
                case PackageStatus.Success:
                    installed++;
                    break;
                case PackageStatus.ChecksumMismatch:
                case PackageStatus.UnparseableVersion:
                case PackageStatus.VersionRegression:
                case PackageStatus.PackageNotFound:
                case PackageStatus.ExtractionFailure:
                default:
                    failed++;
                    break;
            }
        }
        return (installed, failed, skipped);
    }

    /// <summary>
    /// Checks if the <see cref="PackageStatus"/> succeded.
    /// </summary>
    /// <param name="status">The status to check against.</param>
    /// <returns>True, if Success.</returns>
    public static bool IsSuccess(this PackageStatus status) {
        return status == PackageStatus.Success;
    }
}
