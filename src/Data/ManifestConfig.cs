using System;
using Tomlyn.Serialization;

namespace BinGet.Data;

/// <summary>
/// The C# model to write the package's manifest.
/// </summary>
public sealed class ManifestConfig {

    /// <summary>
    /// The name of the package.
    /// </summary>
    [TomlPropertyName("packageName")]
    public string PackageName { get; set; } = string.Empty;

    /// <summary>
    /// The repository the package was downloaded from.
    /// </summary>
    [TomlPropertyName("repository")]
    public string Repository { get; set; } = string.Empty;

    /// <summary>
    /// The version info.
    /// </summary>
    [TomlPropertyName("tag")]
    public string Tag { get; set; } = string.Empty;

    /// <summary>
    /// The actual name of the archive downloaded.
    /// </summary>
    [TomlPropertyName("asset")]
    public string Asset { get; set; } = string.Empty;

    /// <summary>
    /// The hash value.
    /// </summary>
    [TomlPropertyName("checksum")]
    public string Checksum { get; set; } = string.Empty;

    /// <summary>
    /// Date when the package was downloaded and installed.
    /// </summary>
    [TomlPropertyName("installed")]
    public DateTime Installed { get; set; }
}

[TomlSerializable(typeof(ManifestConfig))]
internal partial class TomlManifestConfigContext : TomlSerializerContext { }
