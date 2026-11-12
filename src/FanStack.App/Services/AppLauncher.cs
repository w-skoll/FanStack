using System.ComponentModel;
using System.Diagnostics;
using FanStack.App.Interop;

namespace FanStack.App.Services;

/// <summary>Esito dell'avvio di un'app.</summary>
internal sealed record LaunchResult(bool Success, string? ErrorMessage)
{
    public static readonly LaunchResult Ok = new(true, null);
}

/// <summary>
/// Avvia un collegamento. Si apre il .lnk stesso con la shell (così funzionano anche app dello
/// Store, collegamenti "pubblicizzati" e .url), dopo aver controllato che il bersaglio esista.
/// </summary>
internal static class AppLauncher
{
    public static LaunchResult Launch(string shortcutPath, string displayName)
    {
        if (!File.Exists(shortcutPath))
            return new(false, $"Il collegamento \"{displayName}\" non esiste più.");

        if (string.Equals(Path.GetExtension(shortcutPath), ".lnk", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var info = ShellLink.Read(shortcutPath);
                // Bersaglio su file system mancante (le app dello Store non hanno un percorso: le lasciamo passare).
                if (LooksLikeMissingFile(info.TargetPath))
                    return new(false, $"Il collegamento \"{displayName}\" punta a un file che non esiste più:\n{info.TargetPath}");
            }
            catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or InvalidCastException or UnauthorizedAccessException or IOException)
            {
                Log.Error($"Lettura del collegamento fallita, provo comunque ad avviarlo: {shortcutPath}", ex);
            }
        }

        try
        {
            using var p = Process.Start(new ProcessStartInfo
            {
                FileName = shortcutPath,
                UseShellExecute = true,
                WorkingDirectory = Path.GetDirectoryName(shortcutPath) ?? string.Empty,
            });
            Log.Info($"Avviato: {shortcutPath}");
            return LaunchResult.Ok;
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            // ERROR_CANCELLED: l'utente ha annullato (per esempio la richiesta UAC). Nessun errore da mostrare.
            Log.Info($"Avvio annullato dall'utente: {shortcutPath}");
            return LaunchResult.Ok;
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or FileNotFoundException)
        {
            Log.Error($"Avvio fallito: {shortcutPath}", ex);
            return new(false, $"Impossibile avviare \"{displayName}\".\n{ex.Message}");
        }
    }

    internal static bool LooksLikeMissingFile(string target)
    {
        if (string.IsNullOrWhiteSpace(target))
            return false;
        if (!Path.IsPathFullyQualified(target))
            return false;
        // Unità di rete o rimovibili non pronte: non bloccare, ci penserà la shell.
        if (target.StartsWith(@"\\", StringComparison.Ordinal))
            return false;
        var root = Path.GetPathRoot(target);
        if (root is not null && !Directory.Exists(root))
            return false;
        return !File.Exists(target) && !Directory.Exists(target);
    }

    /// <summary>Apre una cartella in Esplora file.</summary>
    public static void OpenFolder(string folder)
    {
        try
        {
            using var _ = Process.Start(new ProcessStartInfo { FileName = folder, UseShellExecute = true, Verb = "open" });
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            Log.Error($"Impossibile aprire la cartella {folder}", ex);
        }
    }
}
