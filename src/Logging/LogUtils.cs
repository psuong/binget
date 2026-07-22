using System.IO;

namespace BinGet.Logging;

/// <summary>
/// Logging utilities.
/// </summary>
public static class LogUtils {
    /// <summary>
    /// The primary name of the log file.
    /// </summary>
    public const string LogFile = "binget.log";

    /// <summary>
    /// Returns the path to the logfile.
    /// </summary>
    /// <returns>A path on the filesystem.</returns>
    public static string GetLogPath() {
        return Path.Join(Directory.GetCurrentDirectory(), LogFile);
    }

    /// <summary>
    /// A round robin logging backup utility. This moves the most recent
    /// log file to a backup and the previous log file to another backup.
    /// At most there will only be 2 backups.
    /// </summary>
    public static void RotateLogFiles() {
        var cwd = Directory.GetCurrentDirectory();
        var logPath = GetLogPath();

        if (File.Exists(logPath)) {
            // Early out because the log file has not been written to so ZLogger can

            var info = new FileInfo(logPath);
            if (info.Length == 0) {
                return;
            }
        }

        var backup1 = Path.Join(cwd, LogFile + ".1");
        var backup2 = Path.Join(cwd, LogFile + ".2");

        // Delete the oldest backup
        if (File.Exists(backup2)) {
            File.Delete(backup2);
        }

        // Move .1 -> .2
        if (File.Exists(backup1)) {
            File.Move(backup1, backup2);
        }

        // Move original to .1
        if (File.Exists(logPath)) {
            File.Move(logPath, backup1);
        }
    }
}
