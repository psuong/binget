using System.Net.Http;
using System.Threading;

namespace BinGet.Data;

public readonly record struct DownloadArgs(
    HttpClient HttpClient,
    BinGetConfig BinGetConfig,
    RepositoryConfig RepositoryConfig,
    string PackageName,
    SemaphoreSlim Semaphore);