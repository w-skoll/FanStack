using FanStack.Core.Stacks;

namespace FanStack.Core.Tests;

/// <summary>Cartella temporanea per i test, eliminata a fine test.</summary>
internal sealed class TempFolder : IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "FanStackTests", Guid.NewGuid().ToString("N"));

    public TempFolder() => Directory.CreateDirectory(Path);

    public string Touch(string name, FileAttributes? attributes = null)
    {
        var full = System.IO.Path.Combine(Path, name);
        File.WriteAllText(full, "x");
        if (attributes is { } a)
            File.SetAttributes(full, a);
        return full;
    }

    public void Dispose()
    {
        foreach (var f in Directory.EnumerateFiles(Path))
            File.SetAttributes(f, FileAttributes.Normal);
        Directory.Delete(Path, recursive: true);
    }
}

public class StackReaderTests
{
    [Fact]
    public void MissingFolder_ReturnsNotFound()
    {
        var result = StackReader.Read(Path.Combine(Path.GetTempPath(), "FanStackTests", "non-esiste-" + Guid.NewGuid()));
        Assert.Equal(StackReadStatus.NotFound, result.Status);
        Assert.Empty(result.Entries);
        Assert.NotNull(result.ErrorMessage);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void BlankPath_ReturnsError(string? path)
    {
        Assert.Equal(StackReadStatus.Error, StackReader.Read(path).Status);
    }

    [Fact]
    public void EmptyFolder_ReturnsEmpty()
    {
        using var tmp = new TempFolder();
        Assert.Equal(StackReadStatus.Empty, StackReader.Read(tmp.Path).Status);
    }

    [Fact]
    public void FolderWithOnlyNonShortcuts_ReturnsEmpty()
    {
        using var tmp = new TempFolder();
        tmp.Touch("note.txt");
        tmp.Touch("desktop.ini");
        Directory.CreateDirectory(Path.Combine(tmp.Path, "Sotto.lnk"));
        Assert.Equal(StackReadStatus.Empty, StackReader.Read(tmp.Path).Status);
    }

    [Fact]
    public void IgnoresHiddenAndNonShortcutFiles()
    {
        using var tmp = new TempFolder();
        tmp.Touch("Word.lnk");
        tmp.Touch("Segreto.lnk", FileAttributes.Hidden);
        tmp.Touch("Sistema.lnk", FileAttributes.System);
        tmp.Touch(".Punto.lnk");
        tmp.Touch("leggimi.txt");
        tmp.Touch("Steam Gioco.url");

        var result = StackReader.Read(tmp.Path);

        Assert.Equal(StackReadStatus.Ok, result.Status);
        Assert.Equal(new[] { "Steam Gioco", "Word" }, result.Entries.Select(e => e.DisplayName));
    }

    [Fact]
    public void SortsAlphabeticallyCaseInsensitive()
    {
        using var tmp = new TempFolder();
        tmp.Touch("zoom.lnk");
        tmp.Touch("Excel.lnk");
        tmp.Touch("access.lnk");
        tmp.Touch("Blocco note.lnk");

        var names = StackReader.Read(tmp.Path).Entries.Select(e => e.DisplayName);

        Assert.Equal(new[] { "access", "Blocco note", "Excel", "zoom" }, names);
    }

    [Fact]
    public void NumericPrefix_ForcesOrderAndIsHidden()
    {
        using var tmp = new TempFolder();
        tmp.Touch("Alfa.lnk");
        tmp.Touch("02 - Excel.lnk");
        tmp.Touch("01 - Word.lnk");
        tmp.Touch("10 - Teams.lnk");

        var entries = StackReader.Read(tmp.Path).Entries;

        Assert.Equal(new[] { "Word", "Excel", "Teams", "Alfa" }, entries.Select(e => e.DisplayName));
        Assert.Equal(new int?[] { 1, 2, 10, null }, entries.Select(e => e.Order));
        Assert.EndsWith("01 - Word.lnk", entries[0].ShortcutPath);
    }

    [Fact]
    public void QuotedPath_IsAccepted()
    {
        using var tmp = new TempFolder();
        tmp.Touch("Word.lnk");
        Assert.Equal(StackReadStatus.Ok, StackReader.Read("\"" + tmp.Path + "\"").Status);
    }
}

public class EntryNameParserTests
{
    [Theory]
    [InlineData("01 - Word", "Word", 1)]
    [InlineData("1 - Word", "Word", 1)]
    [InlineData("3. Excel", "Excel", 3)]
    [InlineData("12) Teams", "Teams", 12)]
    [InlineData("05 -   Spazi  ", "Spazi", 5)]
    public void ParsesPrefix(string input, string name, int order)
    {
        var (n, o) = EntryNameParser.Parse(input);
        Assert.Equal(name, n);
        Assert.Equal(order, o);
    }

    [Theory]
    [InlineData("7-Zip")]
    [InlineData("3D Builder")]
    [InlineData("2048 - Gioco")]
    [InlineData("Word")]
    [InlineData("01 -")]
    public void LeavesNamesWithoutValidPrefix(string input)
    {
        var (n, o) = EntryNameParser.Parse(input);
        Assert.Equal(input.Trim(), n);
        Assert.Null(o);
    }
}
