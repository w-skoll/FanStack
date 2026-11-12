using System.Windows;
using FanStack.App.Services;
using FanStack.App.UI;
using FanStack.Core.Geometry;
using FanStack.Core.Stacks;

namespace FanStack.App;

/// <summary><c>--stack "cartella"</c>: mostra il ventaglio, avvia l'app scelta, poi termina.</summary>
internal static class StackCommand
{
    /// <summary>
    /// Dopo una chiusura per clic fuori, il processo resta vivo un attimo: se il clic era sulla
    /// stessa icona della taskbar, il nuovo processo lo trova e si chiude (comportamento a interruttore).
    /// </summary>
    private static readonly TimeSpan ToggleLinger = TimeSpan.FromMilliseconds(450);

    public static int Run(string folderPath, string appId)
    {
        using var guard = StackInstanceGuard.TryAcquire(appId);
        if (guard is null)
        {
            Log.Info($"Ventaglio già aperto per {appId}: chiudo quello esistente.");
            return 0;
        }

        var read = StackReader.Read(folderPath);
        if (read.Status is StackReadStatus.NotFound or StackReadStatus.Error)
        {
            Log.Error($"Stack non leggibile: {read.ErrorMessage}");
            MessageBox.Show($"{read.ErrorMessage}\n\nSe hai spostato o rinominato la cartella, ricrea lo stack con:\nFanStack.exe --create \"Nome\"",
                "FanStack", MessageBoxButton.OK, MessageBoxImage.Warning);
            return 1;
        }

        var folder = Path.GetFullPath(folderPath);
        var entries = read.Entries.Count > 0
            ? read.Entries.Select(e => new FanEntry(e.DisplayName, e.ShortcutPath)).ToList()
            : [new FanEntry("Cartella vuota: aggiungi app", null)];

        var screen = ScreenInfo.Capture();
        var theme = Theme.Current();
        var options = new FanLayoutOptions();
        var layout = FanLayoutEngine.Compute(entries.Count, screen.Placement.Origin, screen.Placement.Edge, screen.Placement.WorkArea, options);
        var pixelSize = IconProvider.PixelSizeFor(options.IconSize, screen.Scale);

        // Rendering software: evita la creazione del device Direct3D (circa 120 ms in meno all'avvio)
        // e su una finestra trasparente piccola l'animazione resta fluida quanto in hardware (misurato).
        System.Windows.Media.RenderOptions.ProcessRenderMode = System.Windows.Interop.RenderMode.SoftwareOnly;
        var app = Program.CreateApplication();
        using var worker = new StaWorker("FanStack.Icons");
        var window = new FanWindow(entries, layout, screen, theme, options);

        // Icone in background: la finestra compare subito, le icone arrivano appena pronte.
        for (var i = 0; i < entries.Count; i++)
        {
            var index = i;
            var entry = entries[i];
            var task = entry.IsOpenFolder
                ? worker.Run(() => IconProvider.Get(folder, entry.DisplayName, pixelSize))
                : worker.Run(() => IconProvider.Get(entry.ShortcutPath!, entry.DisplayName, pixelSize));
            task.ContinueWith(t =>
            {
                if (t.IsCompletedSuccessfully)
                    window.Dispatcher.BeginInvoke(() => window.SetIcon(index, t.Result));
                else
                    Log.Error($"Icona non caricata: {entry.DisplayName}", t.Exception);
            }, TaskScheduler.Default);
        }

        Task<LaunchResult>? launch = null;
        window.ItemInvoked += index =>
        {
            var entry = entries[index];
            if (entry.IsOpenFolder)
            {
                AppLauncher.OpenFolder(folder);
                launch = Task.FromResult(LaunchResult.Ok);
            }
            else
            {
                // Avvio subito, in parallelo all'animazione di chiusura.
                launch = worker.Run(() => AppLauncher.Launch(entry.ShortcutPath!, entry.DisplayName));
            }
            window.BeginClose(CloseReason.Launch);
        };

        window.CloseFinished += async reason =>
        {
            if (launch is not null)
            {
                var result = await launch;
                if (!result.Success)
                    Log.Error($"Avvio non riuscito: {result.ErrorMessage}");
                if (!result.Success)
                    MessageBox.Show(result.ErrorMessage, "FanStack", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            // Icona dello stack sulla taskbar sempre allineata al contenuto della cartella.
            try
            {
                var (iconPath, changed) = await worker.Run(() => StackService.UpdateIcon(folder, read.Entries, force: false));
                if (changed)
                    StackService.NotifyShellIconsChanged(iconPath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Runtime.InteropServices.COMException)
            {
                Log.Error("Aggiornamento dell'icona dello stack fallito", ex);
            }

            var linger = reason is CloseReason.Deactivated or CloseReason.OutsideClick ? ToggleLinger : TimeSpan.Zero;
            Program.After(linger, () => app.Shutdown());
        };

        guard.CloseRequested += () => window.Dispatcher.BeginInvoke(() => window.BeginClose(CloseReason.External));
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged += (_, _) =>
            window.Dispatcher.BeginInvoke(() => window.BeginClose(CloseReason.DisplayChanged));

        window.Show();
        return app.Run();
    }
}
