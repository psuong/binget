using ConsoleAppFramework;
using ZLogger;
using System;
using Microsoft.Extensions.Logging;
using System.IO;
using Tomlyn;
using BinGet.Data;
using Cysharp.IO;
using System.Threading.Tasks;
using System.Text;

namespace BinGet;

public class Commands {

    private readonly ILogger<Commands> logger;

    public Commands(ILogger<Commands> logger) {
        this.logger = logger;
    }

    /// <summary>
    /// Parses the toml config file.
    /// </summary>
    /// <param name="config">-c, The configuration file to load.</param>
    [Command("")]
    public async Task ParseConfig(string config) {
        if (!File.Exists(config)) {
            throw new FileNotFoundException($"The config file: {config} does not exist!");
        }
        using Utf8StreamReader streamReader = new Utf8StreamReader(config, FileOpenMode.Throughput);
        byte[] text = await streamReader.ReadToEndAsync();
        Config output = TomlSerializer.Deserialize<Config>(Encoding.UTF8.GetString(text), TomlConfigContext.Default);
        logger.ZLogInformation($"{output}");
    }
}

public class Program {
    public static void Main(string[] args) {
        var app = ConsoleApp.Create().ConfigureLogging(static builder => {
            builder.ClearProviders()
                .SetMinimumLevel(LogLevel.Trace)
                .AddZLoggerConsole()
                .AddZLoggerFile("binget.log");
        });
        app.Add<Commands>();
        app.Run(args);
    }
}
