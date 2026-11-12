namespace FanStack.App.Services;

/// <summary>Cartelle usate dall'app. Si lavora solo in %UserProfile%\FanStacks e %AppData%\FanStack.</summary>
internal static class AppPaths
{
    public static string StacksRoot { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "FanStacks");

    public static string DataRoot { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FanStack");

    /// <summary>Icone .ico generate per gli stack (puntate dai collegamenti).</summary>
    public static string StackIconsDir => Path.Combine(DataRoot, "icons");

    /// <summary>Cache su disco delle icone delle app (PNG). La sottocartella cambia se cambia il formato.</summary>
    public static string IconCacheDir => Path.Combine(DataRoot, "cache", "v2");

    public static string LogPath => Path.Combine(DataRoot, "log.txt");

    public static string Desktop => Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);

    public static string ExecutablePath => Environment.ProcessPath ?? throw new InvalidOperationException("Percorso dell'eseguibile non disponibile.");
}
