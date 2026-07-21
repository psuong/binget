using Scriban.Runtime;
using System;
using System.Net.Http;
using System.Threading;

namespace BinGet.Data;

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
            ["date"] = DateTime.Now.ToString("dd-MM-yyyyTHH:mm:ssZ"),
            ["sha256"] = Sha256
        };
    }
}

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