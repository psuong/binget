using BinGet.Logging;
using ConsoleAppFramework;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace BinGet;

public class Program {
    public static void Main(string[] args) {
        LogUtils.RotateLogFiles();
        var app = ConsoleApp.Create().ConfigureLogging(static builder => {
            builder.ClearProviders()
#if DEBUG
                .SetMinimumLevel(LogLevel.Trace)
#else
                .SetMinimumLevel(LogLevel.Warning)
#endif
                .AddZLoggerFile(LogUtils.LogFile, static options => {
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
