using Cysharp.IO;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Tomlyn;
using Tomlyn.Serialization;

namespace BinGet.Data;

public sealed class BinGetConfig {
    [TomlPropertyName("url")]
    public string Url { get; set; } = string.Empty;
    [TomlPropertyName("destination")]
    public string Destination { get; set; } = string.Empty;
    [TomlPropertyName("repositories")]
    public Dictionary<string, RepositoryConfig> Repositories { get; set; }

    public override string ToString() {
        StringBuilder sb = new StringBuilder(256);
        sb
            .Append("Url: ")
            .AppendLine(Url)
            .Append("Destination: ")
            .AppendLine(Destination);

        Dictionary<string, RepositoryConfig>.Enumerator it = Repositories != null ? Repositories.GetEnumerator() : default;
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
        using Utf8StreamReader streamReader = new Utf8StreamReader(path, FileOpenMode.Throughput);
        byte[] text = await streamReader.ReadToEndAsync();
        return TomlSerializer.Deserialize<BinGetConfig>(Encoding.UTF8.GetString(text), TomlBinGetConfigContext.Default);
    }
}

public sealed class RepositoryConfig {
    [TomlPropertyName("id")]
    public string Id { get; set; } = string.Empty;
    [TomlPropertyName("targetPattern")]
    public string TargetPattern { get; set; } = string.Empty;

    public override string ToString() {
        return $"ID: {Id}, Target Pattern: {TargetPattern}";
    }
}

[TomlSerializable(typeof(BinGetConfig))]
internal partial class TomlBinGetConfigContext : TomlSerializerContext { }
