using System;
using System.Runtime.CompilerServices;

namespace BinGet.Utils;

public static class VersionHelper {

    /// <summary>
    /// Attempst to normalize semantic versioning.
    /// </summary>
    /// <param name="version">The version to format.</param>
    /// <param name="actual">A version object that can be compared to other versions.</param>
    /// <returns>True, if successfully parsed.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryParseVersion(string version, out Version actual) {
        if (version.StartsWith('v')) {
            version = version[1..];
        }
        return Version.TryParse(version, out actual);
    }
}