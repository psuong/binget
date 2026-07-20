using ConsoleAppFramework;
using ZLogger;
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
    private BinGetConfig binGetConfig;

    public Commands(ILogger<Commands> logger) {
        this.logger = logger;
    }

    [Command("")]
    public async Task ParseConfig(string config) {
        if (!File.Exists(config)) {
            throw new FileNotFoundException($"The config file: {config} does not exist!");
        }
        using Utf8StreamReader streamReader = new Utf8StreamReader(config, FileOpenMode.Throughput);
        byte[] text = await streamReader.ReadToEndAsync();
        binGetConfig = TomlSerializer.Deserialize<BinGetConfig>(Encoding.UTF8.GetString(text), TomlBinGetConfigContext.Default);
        logger.ZLogInformation($"{binGetConfig}");
    }

    [Command("install")]
    public void Install() {
    }

    [Command("clean")]
    public void Clean() {
        logger.ZLogInformation($"Cleaning");
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
