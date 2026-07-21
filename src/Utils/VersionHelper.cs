using System;
using System.Runtime.CompilerServices;

namespace BinGet.Utils;

public static class VersionHelper {

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryParseVersion(string version, out Version actual) {
        if (version.StartsWith('v')) {
            version = version[1..];
        }
        return Version.TryParse(version, out actual);
    }
}