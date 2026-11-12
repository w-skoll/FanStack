using System.Windows;
using FanStack.App.Services;
using FanStack.Core.Stacks;

namespace FanStack.App;

/// <summary><c>--refresh-icons</c>: rigenera le icone di tutti gli stack in %UserProfile%\FanStacks.</summary>
internal static class RefreshCommand
{
    public static int Run(bool quiet)
    {
        var count = 0;
        var errors = new List<string>();
        if (Directory.Exists(AppPaths.StacksRoot))
        {
            foreach (var dir in Directory.EnumerateDirectories(AppPaths.StacksRoot))
            {
                try
                {
                    var read = StackReader.Read(dir);
                    var (iconPath, _) = StackService.UpdateIcon(dir, read.Entries, force: true);
                    Log.Info($"Icona rigenerata: {iconPath}");
                    count++;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Runtime.InteropServices.COMException)
                {
                    Log.Error($"Rigenerazione icona fallita per {dir}", ex);
                    errors.Add($"{Path.GetFileName(dir)}: {ex.Message}");
                }
            }
        }

        if (count > 0)
            StackService.NotifyShellIconsChanged(AppPaths.StackIconsDir);

        var message = $"Icone rigenerate: {count}." + (errors.Count > 0 ? "\n\nErrori:\n" + string.Join("\n", errors) : string.Empty);
        if (quiet)
            Console.WriteLine(message);
        else
            MessageBox.Show(message, "FanStack", MessageBoxButton.OK, errors.Count > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information);
        return errors.Count > 0 ? 1 : 0;
    }
}
