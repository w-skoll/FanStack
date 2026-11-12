using System.Globalization;
using System.Text;
using FanStack.Core.Logging;

namespace FanStack.App.Services;

/// <summary>Log semplice in %AppData%\FanStack\log.txt con rotazione (log.1.txt).</summary>
internal static class Log
{
    private static readonly object Gate = new();

    public static void Info(string message) => Write("INFO", message);

    public static void Error(string message, Exception? ex = null) =>
        Write("ERRORE", ex is null ? message : $"{message}: {ex.GetType().Name}: {ex.Message}{Environment.NewLine}{ex.StackTrace}");

    private static void Write(string level, string message)
    {
        var line = string.Create(CultureInfo.InvariantCulture,
            $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{Environment.ProcessId}] {level} {message}{Environment.NewLine}");
        lock (Gate)
        {
            try
            {
                Directory.CreateDirectory(AppPaths.DataRoot);
                var path = AppPaths.LogPath;
                var info = new FileInfo(path);
                if (info.Exists && LogRotation.ShouldRotate(info.Length, Encoding.UTF8.GetByteCount(line)))
                    File.Move(path, Path.Combine(AppPaths.DataRoot, "log.1.txt"), overwrite: true);
                File.AppendAllText(path, line, Encoding.UTF8);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Il log non deve mai far fallire l'app: ripiega sull'output di debug.
                System.Diagnostics.Debug.WriteLine($"Log non scrivibile ({ex.Message}): {line}");
            }
        }
    }
}
