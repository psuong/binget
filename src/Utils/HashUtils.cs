using BinGet.Data;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ZLogger;

namespace BinGet.Utils;

public static partial class HashUtils {
    private static readonly Regex HashPattern = GetHashPatternRegex();

    [GeneratedRegex(@"(\w+):(\w+)")]
    private static partial Regex GetHashPatternRegex();

    /// <summary>
    /// Checks if a stream's checksum is equivalent to the original.
    /// </summary>
    /// <param name="original">The original checksum. The string must be in a format like: {hashType}:{hashValue}.</param>
    /// <param name="stream">The piece of memory to check against.</param>
    /// <param name="logger">The logger to log any issues during the checksum.</param>
    /// <param name="securityLevel">See <see cref="SecurityLevels"/> for more details.</param>
    /// <returns>True, if the hashes match between the <paramref name="original"/> and the <paramref name="stream"/>.
    /// False, if not and if the <paramref name="original"/> cannot be parsed.</returns>
    public static async ValueTask<bool> CompareChecksum<T>(string original, Stream stream, ILogger<T> logger, SecurityLevels securityLevel) {
        var result = true;
        switch (securityLevel) {
            case SecurityLevels.None:
                return result;
            case SecurityLevels.Laxed:
                if (string.IsNullOrEmpty(original)) {
                    logger.ZLogWarning($"No checksum was available to check against, but with laxed security permissions the process succeded.");
                    return true;
                }
                result = await CompareChecksum<T>(original, stream, logger);
                return result;
            case SecurityLevels.Strict:
                if (string.IsNullOrEmpty(original)) {
                    logger.ZLogWarning($"No checksum was available to check against.");
                    return false;
                }
                result = await CompareChecksum<T>(original, stream, logger);
                return result;
        }
        return false;
    }

    /// <summary>
    /// Performs the actual checksum check on a valid hash.
    /// </summary>
    /// <typeparam name="T">The logger type.</typeparam>
    /// <param name="original">The original checksum to check.</param>
    /// <param name="stream">The hash to generate from the file.</param>
    /// <param name="logger">The logger to log any warnings/errors.</param>
    /// <returns>True, if checksum is equivalent.</returns>
    /// <exception cref="NotSupportedException">Currently supported hash functions are MD1, SHA1, SHA256, SHA384, and SHA512.
    /// If the hash you're looking for is not supported, this method will throw.</exception>
    private static async ValueTask<bool> CompareChecksum<T>(string original, Stream stream, ILogger<T> logger) {
        var match = HashPattern.Match(original);
        if (match.Success) {
            var hashType = match.Groups[1].Value;
            var hashValue = match.Groups[2].Value;
            var checksum = Convert.ToHexStringLower(hashType switch {
                "sha256" => await SHA256.HashDataAsync(stream),
                "sha512" => await SHA512.HashDataAsync(stream),
                "sha384" => await SHA384.HashDataAsync(stream),
                "sha1" => await SHA1.HashDataAsync(stream),
                "MD5" => await MD5.HashDataAsync(stream),
                _ => throw new NotSupportedException($"Hash Function: {hashType} is not supported")
            });

            bool result = hashValue == checksum;
            if (!result) {
                logger.ZLogCritical($"Checksum comparison failed! Expected: {hashValue}, but received {checksum} instead!");
            }
            return result;
        }
        return false;
    }
}
