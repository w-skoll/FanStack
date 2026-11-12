using System.Globalization;
using System.Text;

namespace FanStack.Core.Naming;

/// <summary>Esito della validazione di un nome di stack.</summary>
public sealed record NameValidation(bool IsValid, string Name, string? Error);

/// <summary>Regole sui nomi: validazione, AppUserModelID, nomi dei file generati.</summary>
public static class StackNaming
{
    public const int MaxNameLength = 60;
    public const string AppIdPrefix = "FanStack.Stack.";

    // Caratteri non ammessi nei nomi file di Windows (fissi, così Core resta indipendente dal sistema).
    private static readonly char[] InvalidChars = "<>:\"/\\|?*".ToCharArray();

    private static readonly HashSet<string> ReservedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    };

    public static NameValidation Validate(string? name)
    {
        var n = (name ?? string.Empty).Trim();
        if (n.Length == 0)
            return new(false, n, "Il nome non può essere vuoto.");
        if (n.Length > MaxNameLength)
            return new(false, n, $"Il nome è troppo lungo (massimo {MaxNameLength} caratteri).");
        if (n.IndexOfAny(InvalidChars) >= 0 || n.Any(char.IsControl))
            return new(false, n, "Il nome non può contenere questi caratteri: < > : \" / \\ | ? *");
        if (n.EndsWith('.'))
            return new(false, n, "Il nome non può finire con un punto.");
        var stem = n.Split('.')[0].Trim();
        if (ReservedNames.Contains(stem))
            return new(false, n, $"\"{n}\" è un nome riservato da Windows.");
        return new(true, n, null);
    }

    /// <summary>
    /// AppUserModelID univoco e stabile per una cartella stack. Lo stesso valore va scritto nel
    /// collegamento e impostato dal processo: così Windows non raggruppa stack diversi in una sola icona.
    /// </summary>
    public static string AppUserModelId(string stackFolderPath)
    {
        var normalized = NormalizeFolderPath(stackFolderPath);
        var readable = Sanitize(LastSegment(normalized), 40);
        // Massimo 128 caratteri, nessuno spazio: prefisso (15) + nome (≤40) + "." + hash (8).
        return $"{AppIdPrefix}{readable}.{PathHash(normalized)}";
    }

    /// <summary>Nome del file .ico generato per lo stack.</summary>
    public static string IconFileName(string stackFolderPath)
    {
        var normalized = NormalizeFolderPath(stackFolderPath);
        return $"{Sanitize(LastSegment(normalized), 40)}-{PathHash(normalized)}.ico";
    }

    /// <summary>Normalizza il percorso di una cartella: niente virgolette, niente separatore finale.</summary>
    public static string NormalizeFolderPath(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        var p = path.Trim().Trim('"').Trim();
        while (p.Length > 3 && (p.EndsWith('\\') || p.EndsWith('/')))
            p = p[..^1];
        return p;
    }

    /// <summary>Solo lettere e cifre ASCII (accenti rimossi), al massimo <paramref name="maxLength"/> caratteri.</summary>
    public static string Sanitize(string text, int maxLength)
    {
        var sb = new StringBuilder();
        foreach (var c in text.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
                continue;
            if (c is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or (>= '0' and <= '9'))
                sb.Append(c);
            if (sb.Length >= maxLength)
                break;
        }
        return sb.Length == 0 ? "Stack" : sb.ToString();
    }

    private static string LastSegment(string path)
    {
        var idx = path.LastIndexOfAny(['\\', '/']);
        return idx >= 0 ? path[(idx + 1)..] : path;
    }

    /// <summary>Hash breve, stabile e senza distinzione tra maiuscole e minuscole del percorso.</summary>
    private static string PathHash(string normalizedPath) =>
        Hashing.Fnv1a32(normalizedPath.Replace('/', '\\').ToUpperInvariant()).ToString("x8", CultureInfo.InvariantCulture);
}
