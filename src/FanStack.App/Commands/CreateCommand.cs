using System.Windows;
using FanStack.App.Services;
using FanStack.Core.Naming;

namespace FanStack.App;

/// <summary><c>--create "Nome"</c>: crea cartella, icona e collegamento, poi apre la cartella.</summary>
internal static class CreateCommand
{
    public static int Run(string name, bool quiet)
    {
        var validation = StackNaming.Validate(name);
        if (!validation.IsValid)
        {
            Log.Error($"Nome stack non valido \"{name}\": {validation.Error}");
            if (!quiet)
                MessageBox.Show(validation.Error, "FanStack", MessageBoxButton.OK, MessageBoxImage.Warning);
            return 2;
        }

        CreateResult result;
        try
        {
            // Siamo già sul thread STA principale: COM e rendering WPF funzionano qui.
            result = StackService.Create(validation.Name);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Runtime.InteropServices.COMException or ArgumentException)
        {
            Log.Error($"Creazione dello stack \"{name}\" fallita", ex);
            if (!quiet)
                MessageBox.Show($"Non sono riuscito a creare lo stack \"{name}\":\n{ex.Message}", "FanStack", MessageBoxButton.OK, MessageBoxImage.Error);
            return 1;
        }

        if (quiet)
        {
            Console.WriteLine(result.ShortcutPath);
            return 0;
        }

        AppLauncher.OpenFolder(result.FolderPath);
        MessageBox.Show(
            $"Stack \"{validation.Name}\" pronto.\n\n" +
            $"1. Trascina nella cartella che si è appena aperta i collegamenti delle app che vuoi (per esempio dal menu Start o dal Desktop).\n\n" +
            $"2. Sul Desktop trovi il collegamento \"{Path.GetFileNameWithoutExtension(result.ShortcutPath)}\": " +
            "tasto destro, poi \"Mostra altre opzioni\", poi \"Aggiungi alla barra delle applicazioni\".\n\n" +
            "3. Clicca la nuova icona sulla taskbar: le app escono a ventaglio.",
            "FanStack", MessageBoxButton.OK, MessageBoxImage.Information);
        return 0;
    }
}
