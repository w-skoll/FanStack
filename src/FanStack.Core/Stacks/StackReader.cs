namespace FanStack.Core.Stacks;

/// <summary>Legge una cartella stack e restituisce le voci ordinate.</summary>
public static class StackReader
{
    /// <summary>Estensioni considerate collegamenti (.url per i collegamenti tipo Steam).</summary>
    public static readonly IReadOnlySet<string> ShortcutExtensions =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".lnk", ".url" };

    public static StackReadResult Read(string? folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
            return StackReadResult.Failed("Nessuna cartella indicata.");

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(folderPath.Trim().Trim('"'));
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return StackReadResult.Failed($"Percorso non valido: {folderPath} ({ex.Message})");
        }

        if (!Directory.Exists(fullPath))
            return StackReadResult.NotFound(fullPath);

        try
        {
            var entries = new List<StackEntry>();
            foreach (var file in new DirectoryInfo(fullPath).EnumerateFiles())
            {
                if (!IsCandidate(file))
                    continue;
                var (name, order) = EntryNameParser.Parse(Path.GetFileNameWithoutExtension(file.Name));
                if (name.Length == 0)
                    continue;
                entries.Add(new StackEntry(name, file.FullName, order));
            }

            return entries.Count == 0
                ? new StackReadResult(StackReadStatus.Empty, Array.Empty<StackEntry>())
                : new StackReadResult(StackReadStatus.Ok, StackOrdering.Sort(entries));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            return StackReadResult.Failed($"Impossibile leggere la cartella \"{fullPath}\": {ex.Message}");
        }
    }

    internal static bool IsCandidate(FileInfo file)
    {
        if ((file.Attributes & (FileAttributes.Hidden | FileAttributes.System)) != 0)
            return false;
        if (file.Name.StartsWith('.'))
            return false;
        return ShortcutExtensions.Contains(file.Extension);
    }
}
