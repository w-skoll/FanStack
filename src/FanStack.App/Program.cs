using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;
using FanStack.App.Interop;
using FanStack.App.Services;
using FanStack.App.UI;
using FanStack.Core;
using FanStack.Core.Naming;

namespace FanStack.App;

/// <summary>Punto di ingresso: interpreta la riga di comando e avvia il comando richiesto.</summary>
public static class Program
{
    /// <summary>Istante di avvio del processo, per misurare il tempo di comparsa del ventaglio.</summary>
    internal static DateTime StartTime { get; } = SafeStartTime();

    [STAThread]
    public static int Main(string[] args)
    {
        var command = CommandLine.Parse(args);

        // L'AppUserModelID va impostato prima di qualsiasi finestra.
        var appId = command is AppCommand.OpenStack open
            ? StackNaming.AppUserModelId(open.FolderPath)
            : "FanStack.App";
        var hr = NativeMethods.SetCurrentProcessExplicitAppUserModelID(appId);
        if (hr < 0)
            Log.Error($"SetCurrentProcessExplicitAppUserModelID fallita (0x{hr:X8}) per {appId}");

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            Log.Error("Eccezione non gestita", e.ExceptionObject as Exception);

        try
        {
            return command switch
            {
                AppCommand.OpenStack o => StackCommand.Run(o.FolderPath, appId),
                AppCommand.CreateStack c => CreateCommand.Run(c.Name, c.Quiet),
                AppCommand.RefreshIcons r => RefreshCommand.Run(r.Quiet),
                AppCommand.Invalid i => ShowInvalid(i.Message),
                _ => HelpCommand.Run(),
            };
        }
        catch (Exception ex)
        {
            Log.Error("Errore inatteso", ex);
            MessageBox.Show($"Si è verificato un errore inatteso:\n{ex.Message}\n\nDettagli nel log:\n{AppPaths.LogPath}",
                "FanStack", MessageBoxButton.OK, MessageBoxImage.Error);
            return 1;
        }
    }

    private static int ShowInvalid(string message)
    {
        Log.Error($"Argomenti non validi: {message}");
        MessageBox.Show($"{message}\n\nUso:\n  FanStack.exe --create \"Nome\"\n  FanStack.exe --stack \"cartella\"\n  FanStack.exe --refresh-icons",
            "FanStack", MessageBoxButton.OK, MessageBoxImage.Warning);
        return 2;
    }

    /// <summary>Crea l'oggetto Application con gestione centralizzata degli errori della UI.</summary>
    internal static Application CreateApplication()
    {
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.DispatcherUnhandledException += (_, e) =>
        {
            Log.Error("Eccezione nella UI", e.Exception);
            e.Handled = true;
            Application.Current?.Shutdown(1);
        };
        return app;
    }

    private static DateTime SafeStartTime()
    {
        try
        {
            using var p = Process.GetCurrentProcess();
            return p.StartTime;
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException)
        {
            Debug.WriteLine($"Ora di avvio non disponibile: {ex.Message}");
            return DateTime.Now;
        }
    }

    /// <summary>Esegue <paramref name="action"/> sul dispatcher dopo <paramref name="delay"/> senza bloccare la UI.</summary>
    internal static void After(TimeSpan delay, Action action)
    {
        var timer = new DispatcherTimer { Interval = delay };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            action();
        };
        timer.Start();
    }
}
