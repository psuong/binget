namespace BinGet.Data;

/// <summary>
/// Stores metadata from the a json response object about the package downloaded.
/// </summary>
/// <param name="FileName">The name of the file.</param>
/// <param name="DownloadUrl">The location of the archive.</param>
/// <param name="Sha256">The Sha256 hash of the archive.</param>
/// <param name="Tag">The semantic versioning number.</param>
public readonly record struct PackageInfo(
    string FileName, 
    string DownloadUrl,
    string Sha256,
    string Tag) {
    public override string ToString() {
        return $"FileName: {FileName}, DownloadUrl: {DownloadUrl}, Sha256 Digest: {Sha256}, Tag: {Tag}";
    }
}
