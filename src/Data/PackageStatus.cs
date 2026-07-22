using System.Collections.Generic;

namespace BinGet.Data;

public enum PackageStatus {
    Success = 1,
    ChecksumMismatch,
    UnparseableVersion,
    VersionRegression,
    PackageNotFound,
    ExtractionFailure,
    Skipped
}

public static class PackageStatusExtensions {
    public static string Format(this PackageStatus status) {
        return status switch {
            PackageStatus.Success => "[blue]✓ - Installed[/]",
            PackageStatus.Skipped => "[purple]>> - Skipped, Version Equivalent[/]",
            PackageStatus.ChecksumMismatch => "[red]✗ - Checksum Mismatch[/]",
            PackageStatus.UnparseableVersion => "[red]✗ - Version Number Corrupted[/]",
            PackageStatus.VersionRegression => "[red]✗ - Version Regression[/]",
            PackageStatus.PackageNotFound => "[red]✗ - Package Not Found[/]",
            PackageStatus.ExtractionFailure => "[red]✗ - Package Extraction Failure[/]",
            _ => "[grey]? - Unknown[/]",
        };
    }

    public static (int installed, int failed, int skipped) CountStatus<T>(
        this IReadOnlyList<(T, PackageStatus)> collection) {
        var installed = 0;
        var failed = 0;
        int skipped = 0;
        for (var i = 0; i < collection.Count; i++) {
            (var _, var status) = collection[i];
            switch (status) {
                case PackageStatus.Skipped:
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

    public static bool IsSuccess(this PackageStatus status) {
        return status == PackageStatus.Success;
    }
}