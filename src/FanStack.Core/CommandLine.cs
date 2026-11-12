namespace FanStack.Core;

/// <summary>Comando richiesto dalla riga di comando.</summary>
public abstract record AppCommand
{
    /// <summary>Nessun argomento: finestra di aiuto.</summary>
    public sealed record ShowHelp : AppCommand;

    /// <summary><c>--stack "cartella"</c>: apre il ventaglio.</summary>
    public sealed record OpenStack(string FolderPath) : AppCommand;

    /// <summary><c>--create "Nome"</c>: crea cartella, icona e collegamento.</summary>
    public sealed record CreateStack(string Name, bool Quiet) : AppCommand;

    /// <summary><c>--refresh-icons</c>: rigenera le icone di tutti gli stack.</summary>
    public sealed record RefreshIcons(bool Quiet) : AppCommand;

    /// <summary>Argomenti non validi.</summary>
    public sealed record Invalid(string Message) : AppCommand;
}

/// <summary>Interpreta gli argomenti della riga di comando.</summary>
public static class CommandLine
{
    public static AppCommand Parse(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);
        if (args.Count == 0)
            return new AppCommand.ShowHelp();

        var quiet = args.Any(a => Is(a, "--quiet"));
        var rest = args.Where(a => !Is(a, "--quiet")).ToList();
        if (rest.Count == 0)
            return new AppCommand.ShowHelp();

        var first = rest[0];
        if (Is(first, "--help") || Is(first, "-h") || Is(first, "/?"))
            return new AppCommand.ShowHelp();

        if (Is(first, "--stack"))
        {
            if (rest.Count < 2 || string.IsNullOrWhiteSpace(rest[1]))
                return new AppCommand.Invalid("Manca il percorso della cartella dopo --stack.");
            return rest.Count > 2
                ? new AppCommand.Invalid($"Argomento non riconosciuto: {rest[2]}")
                : new AppCommand.OpenStack(rest[1].Trim().Trim('"'));
        }

        if (Is(first, "--create"))
        {
            if (rest.Count < 2 || string.IsNullOrWhiteSpace(rest[1]))
                return new AppCommand.Invalid("Manca il nome dello stack dopo --create.");
            return rest.Count > 2
                ? new AppCommand.Invalid($"Argomento non riconosciuto: {rest[2]}")
                : new AppCommand.CreateStack(rest[1].Trim().Trim('"'), quiet);
        }

        if (Is(first, "--refresh-icons"))
            return rest.Count > 1
                ? new AppCommand.Invalid($"Argomento non riconosciuto: {rest[1]}")
                : new AppCommand.RefreshIcons(quiet);

        return new AppCommand.Invalid($"Argomento non riconosciuto: {first}");
    }

    /// <summary>Riga di argomenti da scrivere nel collegamento di uno stack.</summary>
    public static string StackArguments(string folderPath) => $"--stack \"{folderPath.TrimEnd('\\')}\"";

    private static bool Is(string arg, string expected) => string.Equals(arg, expected, StringComparison.OrdinalIgnoreCase);
}
