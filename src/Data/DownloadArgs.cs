using System.Net.Http;
using System.Threading;

namespace BinGet.Data;

public readonly record struct PackageInfo(
    string FileName, 
    string DownloadUrl,
    string Sha256,
    string Tag) {
    public override string ToString() {
        return $"FileName: {FileName}, DownloadUrl: {DownloadUrl}, Sha256 Digest: {Sha256}, Tag: {Tag}";
    }
}

public readonly record struct DownloadArgs(
    HttpClient HttpClient,
    BinGetConfig BinGetConfig,
    RepositoryConfig RepositoryConfig,
    string PackageName,
    SemaphoreSlim Semaphore);