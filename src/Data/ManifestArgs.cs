using Scriban.Runtime;
using System;

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
            ["date"] = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss"),
            ["sha256"] = Sha256
        };
    }
}
