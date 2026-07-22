using BinGet.Pool;
using BinGet.Templates;
using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace BinGet.Data;

/// <summary>
/// Data that needs to be written to a manifest.toml that describes the package.
/// </summary>
/// <param name="Destination">The directory the package is located in.</param>
/// <param name="PackageName">The name of the package.</param>
/// <param name="ZipPath">The path of the downloaded archive.</param>
/// <param name="Repository">The repository the package was fetched from.</param>
/// <param name="Asset">The name of the actual asset downloaded.</param>
/// <param name="Checksum">The Checksum hash of the archive.</param>
/// <param name="TagName">The semantic version of the package archive.</param>
public readonly record struct ManifestArgs(
    string Destination,
    string PackageName,
    string ZipPath,
    string Repository,
    string Asset,
    string Checksum,
    string TagName) : ITemplate {

    public async void Format(StringBuilder stringBuilder, string path) {
        using var _ = new ObjectPoolScope<StringBuilder>(
            static () => new StringBuilder(256),
            static sb => sb.Clear(),
            out var sb);
        sb.Append("package = ").Append('"').Append(PackageName).Append('"').AppendLine();
        sb.Append("repository = ").Append('"').Append(Repository).Append('"').AppendLine();
        sb.Append("tag = ").Append('"').Append(TagName).Append('"').AppendLine();
        sb.Append("asset = ").Append('"').Append(Asset).Append('"').AppendLine();
        sb.Append("checksum = ").Append('"').Append(Checksum).Append('"').AppendLine();
        sb.Append("installed = ").Append(DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture)).AppendLine();
        await File.WriteAllTextAsync(path, sb.ToString());
    }
}
