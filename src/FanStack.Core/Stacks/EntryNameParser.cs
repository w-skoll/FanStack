using System.Globalization;
using System.Text.RegularExpressions;

namespace FanStack.Core.Stacks;

/// <summary>
/// Ricava nome visualizzato e ordine forzato dal nome file di un collegamento.
/// "01 - Word" diventa ("Word", 1). "7-Zip" resta "7-Zip" (serve uno spazio dopo il separatore).
/// </summary>
public static partial class EntryNameParser
{
    // Da 1 a 3 cifre, separatore (-, ., ")"), almeno uno spazio, poi il nome.
    [GeneratedRegex(@"^(?<order>\d{1,3})\s*[-.)]\s+(?<name>\S.*)$", RegexOptions.CultureInvariant)]
    private static partial Regex OrderPrefix();

    public static (string DisplayName, int? Order) Parse(string fileNameWithoutExtension)
    {
        ArgumentNullException.ThrowIfNull(fileNameWithoutExtension);
        var trimmed = fileNameWithoutExtension.Trim();
        var match = OrderPrefix().Match(trimmed);
        if (match.Success)
        {
            var name = match.Groups["name"].Value.Trim();
            return (name, int.Parse(match.Groups["order"].Value, CultureInfo.InvariantCulture));
        }
        return (trimmed, null);
    }
}

/// <summary>
/// Ordinamento delle voci: prima quelle con prefisso numerico (per numero), poi le altre
/// in ordine alfabetico senza distinzione tra maiuscole e minuscole.
/// </summary>
public static class StackOrdering
{
    private static readonly StringComparer NameComparer = StringComparer.InvariantCultureIgnoreCase;

    public static IReadOnlyList<StackEntry> Sort(IEnumerable<StackEntry> entries) =>
        entries
            .OrderBy(e => e.Order.HasValue ? 0 : 1)
            .ThenBy(e => e.Order ?? 0)
            .ThenBy(e => e.DisplayName, NameComparer)
            .ThenBy(e => e.ShortcutPath, StringComparer.OrdinalIgnoreCase)
            .ToList();
}
