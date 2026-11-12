namespace FanStack.Core.Stacks;

/// <summary>Una voce dello stack: un collegamento dentro la cartella.</summary>
/// <param name="DisplayName">Nome mostrato (senza estensione e senza prefisso numerico).</param>
/// <param name="ShortcutPath">Percorso completo del collegamento.</param>
/// <param name="Order">Prefisso numerico di ordinamento, se presente.</param>
public sealed record StackEntry(string DisplayName, string ShortcutPath, int? Order);

/// <summary>Esito della lettura di una cartella stack.</summary>
public enum StackReadStatus
{
    /// <summary>Cartella letta, contiene almeno una voce.</summary>
    Ok,
    /// <summary>Cartella esistente ma senza collegamenti validi.</summary>
    Empty,
    /// <summary>La cartella non esiste.</summary>
    NotFound,
    /// <summary>Percorso non valido o cartella non leggibile.</summary>
    Error,
}

/// <summary>Risultato esplicito della lettura di uno stack.</summary>
public sealed record StackReadResult(StackReadStatus Status, IReadOnlyList<StackEntry> Entries, string? ErrorMessage = null)
{
    public static StackReadResult NotFound(string path) =>
        new(StackReadStatus.NotFound, Array.Empty<StackEntry>(), $"La cartella \"{path}\" non esiste.");

    public static StackReadResult Failed(string message) =>
        new(StackReadStatus.Error, Array.Empty<StackEntry>(), message);
}
