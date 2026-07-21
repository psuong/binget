using Scriban.Runtime;
using System;
using System.Globalization;

namespace BinGet.Data;

/// <summary>
/// Data that needs to be written to a manifest.toml that describes the package.
/// </summary>
/// <param name="Destination">The directory the package is located in.</param>
/// <param name="PackageName">The name of the package.</param>
/// <param name="ZipPath">The path of the downloaded archive.</param>
/// <param name="Repository">The repository the package was fetched from.</param>
/// <param name="Asset">The name of the actual asset downloaded.</param>
/// <param name="Sha256">The Sha256 hash of the archive.</param>
/// <param name="TagName">The semantic version of the package archive.</param>
public readonly record struct ManifestArgs(
    string Destination,
    string PackageName,
    string ZipPath,
    string Repository,
    string Asset,
    string Sha256,
    string TagName) {
    public readonly ScriptObject ToScriptObject() {
        return new ScriptObject {
            ["packageName"] = PackageName,
            ["repository"] = Repository,
            ["tag"] = TagName,
            ["asset"] = Asset,
            ["date"] = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture),
            ["sha256"] = Sha256
        };
    }
}
