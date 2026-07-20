using ConsoleAppFramework;
using ZLogger;
using Microsoft.Extensions.Logging;
using BinGet.Data;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.IO;

namespace BinGet;

public class Commands {

    private readonly ILogger<Commands> logger;

    public Commands(ILogger<Commands> logger) {
        this.logger = logger;
    }

    [Command("install")]
    public async Task Install(string config) {
        BinGetConfig toml = await BinGetConfig.Load(config);
        if (!Directory.Exists(toml.Destination)) {
            logger.ZLogInformation($"Creating the directory at path {toml.Destination}");
            Directory.CreateDirectory(toml.Destination);
        }
        
        Dictionary<string, RepositoryConfig>.Enumerator it = toml.Repositories.GetEnumerator();
        while (it.MoveNext()) {
            (string packageName, RepositoryConfig _) = it.Current;
            string packagePath = Path.Join(toml.Destination, packageName);
        }
    }

    /// <summary>
    /// Removes any local packages that are not listed in the config file. Your config file is effectively your primary list.
    /// </summary>
    /// <param name="config">The configuration toml file that lists the packages.</param>
    /// <param name="updatePath">Updates the path variables.</param>
    [Command("clean")]
    public async Task Clean(string config, bool updatePath) {
        BinGetConfig toml = await BinGetConfig.Load(config);
        if (!Directory.Exists(toml.Destination)) {
            logger.ZLogInformation($"Creating the directory at path {toml.Destination}");
            Directory.CreateDirectory(toml.Destination);
        }
        
        HashSet<string> packagePaths = [.. Directory.GetDirectories(toml.Destination)];
        Dictionary<string, RepositoryConfig>.Enumerator it = toml.Repositories.GetEnumerator();
        while (it.MoveNext()) {
            (string packageName, RepositoryConfig _) = it.Current;
            string packagePath = Path.Join(toml.Destination, packageName);
            packagePaths.Remove(packagePath);
        }

        HashSet<string>.Enumerator remainingIt = packagePaths.GetEnumerator();
        while (remainingIt.MoveNext()) {
            logger.ZLogInformation($"Removing package at: {remainingIt.Current}");
            Directory.Delete(remainingIt.Current);
            // TODO: Update the path variables
        }
    }
}

public class Program {
    public static void Main(string[] args) {
        var app = ConsoleApp.Create().ConfigureLogging(static builder => {
            builder.ClearProviders()
                .SetMinimumLevel(LogLevel.Trace)
                .AddZLoggerConsole(static options => {
                    options.UsePlainTextFormatter(static formatter => {
                        formatter.SetPrefixFormatter($"{0}|{1}| ", static (in MessageTemplate template, in LogInfo info) => template.Format(info.Timestamp, info.LogLevel));
                        formatter.SetSuffixFormatter($" ({0})", static (in MessageTemplate template, in LogInfo info) => template.Format(info.Category));
                        formatter.SetExceptionFormatter(static (writer, ex) => Utf8StringInterpolation.Utf8String.Format(writer, $"{ex.Message}"));
                    });
                })
                .AddZLoggerFile("binget.log");
        });
        app.Add<Commands>();
        app.Run(args);
    }
}
