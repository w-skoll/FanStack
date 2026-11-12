using System.Globalization;
using System.Text;

namespace FanStack.Core;

/// <summary>Hash FNV-1a: veloce, stabile tra esecuzioni (a differenza di string.GetHashCode).</summary>
public static class Hashing
{
    public static uint Fnv1a32(string text)
    {
        var hash = 2166136261u;
        foreach (var b in Encoding.UTF8.GetBytes(text))
        {
            hash ^= b;
            hash *= 16777619u;
        }
        return hash;
    }

    public static ulong Fnv1a64(string text)
    {
        var hash = 14695981039346656037ul;
        foreach (var b in Encoding.UTF8.GetBytes(text))
        {
            hash ^= b;
            hash *= 1099511628211ul;
        }
        return hash;
    }

    public static string Hex64(string text) => Fnv1a64(text).ToString("x16", CultureInfo.InvariantCulture);
}

/// <summary>Chiavi per la cache delle icone e per sapere quando rigenerare l'icona di uno stack.</summary>
public static class CacheKeys
{
    /// <summary>Chiave dell'icona di un file: cambia se cambia il file (dimensione o data di modifica).</summary>
    public static string IconKey(string filePath, long length, DateTime lastWriteUtc, int pixelSize) =>
        Hashing.Hex64(string.Join('|',
            filePath.ToUpperInvariant(),
            length.ToString(CultureInfo.InvariantCulture),
            lastWriteUtc.Ticks.ToString(CultureInfo.InvariantCulture),
            pixelSize.ToString(CultureInfo.InvariantCulture)));

    /// <summary>Firma delle voci mostrate nell'anteprima dell'icona dello stack.</summary>
    public static string StackIconSignature(IEnumerable<(string Path, DateTime LastWriteUtc)> previewEntries, int rendererVersion) =>
        Hashing.Hex64(string.Join('|', previewEntries
            .Select(e => e.Path.ToUpperInvariant() + "@" + e.LastWriteUtc.Ticks.ToString(CultureInfo.InvariantCulture))
            .Prepend("v" + rendererVersion.ToString(CultureInfo.InvariantCulture))));
}
