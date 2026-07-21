using Cysharp.IO;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Tomlyn;
using Tomlyn.Serialization;

namespace BinGet.Data;

/// <summary>
/// The C# object model for the configuration toml file.
/// </summary>
public sealed class BinGetConfig {
    /// <summary>
    /// The root url to fetch the packages from.
    /// </summary>
    [TomlPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// The location to write the packages to.
    /// </summary>
    [TomlPropertyName("destination")]
    public string Destination { get; set; } = string.Empty;

    /// <summary>
    /// Repository metadata.
    /// </summary>
    [TomlPropertyName("repositories")]
    public Dictionary<string, RepositoryConfig> Repositories { get; set; }

    public override string ToString() {
        var sb = new StringBuilder(256);
        sb
            .Append("Url: ")
            .AppendLine(Url)
            .Append("Destination: ")
            .AppendLine(Destination);

        var it = Repositories != null ? Repositories.GetEnumerator() : default;
        while (it.MoveNext()) {
            sb.AppendLine(it.Current.Key)
                .AppendLine(it.Current.Value.ToString());
        }
        return sb.ToString();
    }

    public static async Task<BinGetConfig> Load(string path) {
        if (!File.Exists(path)) {
            throw new FileNotFoundException($"The config file: {path} does not exist!");
        }
        using var streamReader = new Utf8StreamReader(path, FileOpenMode.Throughput);
        var text = await streamReader.ReadToEndAsync();
        return TomlSerializer.Deserialize<BinGetConfig>(Encoding.UTF8.GetString(text), TomlBinGetConfigContext.Default);
    }
}

/// <summary>
/// The C# object model for the repository toml configuration.
/// </summary>
public sealed class RepositoryConfig {
    /// <summary>
    /// The unique identifier for the repository.
    /// </summary>
    [TomlPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// A regex to search the pattern.
    /// </summary>
    [TomlPropertyName("targetPattern")]
    public string TargetPattern { get; set; } = string.Empty;

    public override string ToString() {
        return $"ID: {Id}, Target Pattern: {TargetPattern}";
    }
}

[TomlSerializable(typeof(BinGetConfig))]
internal partial class TomlBinGetConfigContext : TomlSerializerContext { }
