using System;
using Tomlyn.Serialization;

namespace BinGet.Data;

public sealed class ManifestConfig {
    [TomlPropertyName("packageName")]
    public string PackageName { get; set; } = string.Empty;

    [TomlPropertyName("repository")]
    public string Repository { get; set; } = string.Empty;

    [TomlPropertyName("tag")]
    public string Tag { get; set; } = string.Empty;

    [TomlPropertyName("asset")]
    public string Asset { get; set; } = string.Empty;

    [TomlPropertyName("sha256")]
    public string Sha256 { get; set; } = string.Empty;

    [TomlPropertyName("installed")]
    public DateTime Installed { get; set; }
}

[TomlSerializable(typeof(ManifestConfig))]
internal partial class TomlManifestConfigContext : TomlSerializerContext { }
