using ConsoleAppFramework;
using ZLogger;
using Microsoft.Extensions.Logging;

namespace BinGet;

class Program {
    public static void Main(string[] args) {
        var app = ConsoleApp.Create().ConfigureLogging(static builder => {
            builder.ClearProviders()
#if DEBUG
                .SetMinimumLevel(LogLevel.Trace)
#else
                .SetMinimumLevel(LogLevel.Warning)
#endif
                .AddZLoggerFile("binget.log", static options => {
                    options.UsePlainTextFormatter(static formatter => {
                        formatter.SetPrefixFormatter($"{0}|{1}| ", static (in MessageTemplate template, in LogInfo info) => template.Format(info.Timestamp, info.LogLevel));
                        formatter.SetSuffixFormatter($" ({0})", static (in MessageTemplate template, in LogInfo info) => template.Format(info.Category));
                        formatter.SetExceptionFormatter(static (writer, ex) => Utf8StringInterpolation.Utf8String.Format(writer, $"{ex.Message}"));
                    });
                });
        });
        app.Add<PackageManager>();
        app.Run(args);
    }
}
