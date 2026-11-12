using FanStack.App.Interop;
using FanStack.Core;
using FanStack.Core.Naming;
using FanStack.Core.Stacks;

namespace FanStack.App.Services;

/// <summary>Esito della creazione di uno stack.</summary>
internal sealed record CreateResult(string FolderPath, string ShortcutPath, string IconPath, string AppUserModelId, bool FolderExisted);

/// <summary>Creazione degli stack e aggiornamento delle loro icone.</summary>
internal static class StackService
{
    private const int PreviewCount = 4;
    private const int PreviewPixelSize = 128;

    public static string IconPathFor(string folder) => Path.Combine(AppPaths.StackIconsDir, StackNaming.IconFileName(folder));

    private static string SignaturePathFor(string folder) => Path.ChangeExtension(IconPathFor(folder), ".sig");

    /// <summary>
    /// Crea (o completa) uno stack: cartella, icona .ico e collegamento sul Desktop con AppUserModelID univoco.
    /// Non elimina mai file esistenti dell'utente. Da chiamare su un thread STA.
    /// </summary>
    public static CreateResult Create(string name)
    {
        var validation = StackNaming.Validate(name);
        if (!validation.IsValid)
            throw new ArgumentException(validation.Error, nameof(name));
        name = validation.Name;

        var folder = Path.Combine(AppPaths.StacksRoot, name);
        var existed = Directory.Exists(folder);
        Directory.CreateDirectory(folder);

        var read = StackReader.Read(folder);
        var iconPath = UpdateIcon(folder, read.Entries, force: true).IconPath;

        var appId = StackNaming.AppUserModelId(folder);
        var lnkPath = ChooseShortcutPath(name, appId);
        ShellLink.Create(
            lnkPath,
            targetPath: AppPaths.ExecutablePath,
            arguments: CommandLine.StackArguments(folder),
            workingDirectory: folder,
            iconPath: iconPath,
            description: $"Stack FanStack \"{name}\": clic per aprire il ventaglio.",
            appUserModelId: appId);
        NotifyShellUpdated(lnkPath);

        Log.Info($"Stack creato: {name} cartella={folder} collegamento={lnkPath} appId={appId}");
        return new CreateResult(folder, lnkPath, iconPath, appId, existed);
    }

    /// <summary>
    /// Rigenera l'icona dello stack se le app in anteprima sono cambiate (o se forzato).
    /// Restituisce il percorso dell'icona e se è stata riscritta. Da chiamare su un thread STA.
    /// </summary>
    public static (string IconPath, bool Changed) UpdateIcon(string folder, IReadOnlyList<StackEntry> entries, bool force)
    {
        var iconPath = IconPathFor(folder);
        var sigPath = SignaturePathFor(folder);
        var preview = entries.Take(PreviewCount).ToList();
        var signature = CacheKeys.StackIconSignature(
            preview.Select(e => (e.ShortcutPath, SafeLastWrite(e.ShortcutPath))), StackIconRenderer.Version);

        if (!force && File.Exists(iconPath) && File.Exists(sigPath) && File.ReadAllText(sigPath).Trim() == signature)
            return (iconPath, false);

        var images = preview.Select(e => IconProvider.Get(e.ShortcutPath, e.DisplayName, PreviewPixelSize)).ToList();
        var bytes = StackIconRenderer.RenderIco(images);

        Directory.CreateDirectory(AppPaths.StackIconsDir);
        var tmp = iconPath + ".tmp";
        File.WriteAllBytes(tmp, bytes);
        File.Move(tmp, iconPath, overwrite: true);
        File.WriteAllText(sigPath, signature);
        Log.Info($"Icona dello stack aggiornata: {iconPath}");
        return (iconPath, true);
    }

    /// <summary>
    /// Chiede a Explorer di ricaricare le icone (anche quella fissata sulla taskbar).
    /// SHCNE_ASSOCCHANGED svuota la cache delle icone: lo usiamo solo quando un'icona cambia davvero.
    /// </summary>
    public static void NotifyShellIconsChanged(string iconPath)
    {
        NativeMethods.SHChangeNotify(NativeMethods.SHCNE_UPDATEITEM, NativeMethods.SHCNF_PATHW | NativeMethods.SHCNF_FLUSHNOWAIT, iconPath, IntPtr.Zero);
        NativeMethods.SHChangeNotify(NativeMethods.SHCNE_ASSOCCHANGED, NativeMethods.SHCNF_IDLIST | NativeMethods.SHCNF_FLUSHNOWAIT, IntPtr.Zero, IntPtr.Zero);
    }

    private static void NotifyShellUpdated(string path) =>
        NativeMethods.SHChangeNotify(NativeMethods.SHCNE_UPDATEITEM, NativeMethods.SHCNF_PATHW | NativeMethods.SHCNF_FLUSHNOWAIT, path, IntPtr.Zero);

    /// <summary>
    /// "Nome.lnk" sul Desktop. Se esiste già un file con quel nome che non è un nostro collegamento
    /// per questo stack, non lo tocchiamo e usiamo "Nome (FanStack).lnk".
    /// </summary>
    private static string ChooseShortcutPath(string name, string appId)
    {
        var desktop = AppPaths.Desktop;
        foreach (var candidate in new[] { $"{name}.lnk", $"{name} (FanStack).lnk" })
        {
            var path = Path.Combine(desktop, candidate);
            if (!File.Exists(path) || IsOurShortcut(path, appId))
                return path;
        }
        for (var i = 2; ; i++)
        {
            var path = Path.Combine(desktop, $"{name} (FanStack {i}).lnk");
            if (!File.Exists(path) || IsOurShortcut(path, appId))
                return path;
        }
    }

    private static bool IsOurShortcut(string lnkPath, string appId)
    {
        try
        {
            return string.Equals(ShellLink.Read(lnkPath).AppUserModelId, appId, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or UnauthorizedAccessException or IOException or InvalidCastException)
        {
            Log.Error($"Collegamento esistente non leggibile: {lnkPath}", ex);
            return false;
        }
    }

    private static DateTime SafeLastWrite(string path)
    {
        try
        {
            return File.GetLastWriteTimeUtc(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Error($"Data di modifica non leggibile: {path}", ex);
            return DateTime.MinValue;
        }
    }
}
