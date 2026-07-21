using System.Net.Http;
using System.Threading;

namespace BinGet.Data;

/// <summary>
/// Metadata to help with downloading the archive.
/// </summary>
/// <param name="HttpClient">The web client responsible for making the request and fetching the data.</param>
/// <param name="BinGetConfig">The primary configuration file.</param>
/// <param name="RepositoryConfig">The configuration object of the package.</param>
/// <param name="PackageName">The name of the package.</param>
/// <param name="Semaphore">A semaphore to limit the downloads.</param>
public readonly record struct DownloadArgs(
    HttpClient HttpClient,
    BinGetConfig BinGetConfig,
    RepositoryConfig RepositoryConfig,
    string PackageName,
    SemaphoreSlim Semaphore);