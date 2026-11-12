using FanStack.Core.Geometry;
using FanStack.Core.Imaging;
using FanStack.Core.Logging;
using FanStack.Core.Naming;

namespace FanStack.Core.Tests;

public class StackNamingTests
{
    [Theory]
    [InlineData("Ufficio")]
    [InlineData("  Giochi  ")]
    [InlineData("Città & Co.mm")]
    [InlineData("Lavoro 2026")]
    public void ValidNames(string name)
    {
        var v = StackNaming.Validate(name);
        Assert.True(v.IsValid, v.Error);
        Assert.Equal(name.Trim(), v.Name);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("a/b")]
    [InlineData("a\\b")]
    [InlineData("che?")]
    [InlineData("x*y")]
    [InlineData("CON")]
    [InlineData("nul.txt")]
    [InlineData("finisce.")]
    [InlineData("tab\tqui")]
    public void InvalidNames(string? name)
    {
        var v = StackNaming.Validate(name);
        Assert.False(v.IsValid);
        Assert.False(string.IsNullOrEmpty(v.Error));
    }

    [Fact]
    public void TooLongName_IsInvalid() =>
        Assert.False(StackNaming.Validate(new string('a', StackNaming.MaxNameLength + 1)).IsValid);

    [Fact]
    public void AppId_IsStable_CaseInsensitive_AndIgnoresTrailingSlashAndQuotes()
    {
        var a = StackNaming.AppUserModelId(@"C:\Users\ale\FanStacks\Ufficio");
        Assert.Equal(a, StackNaming.AppUserModelId(@"C:\Users\ale\FanStacks\Ufficio\"));
        // Stessa cartella scritta con maiuscole diverse: stesso hash (la parte leggibile segue il testo).
        Assert.Equal(a, StackNaming.AppUserModelId(@"c:\users\ALE\fanstacks\ufficio"), ignoreCase: true);
        Assert.Equal(a, StackNaming.AppUserModelId("\"C:\\Users\\ale\\FanStacks\\Ufficio\""));
        Assert.StartsWith("FanStack.Stack.Ufficio.", a);
    }

    [Fact]
    public void AppId_DiffersBetweenStacks()
    {
        var a = StackNaming.AppUserModelId(@"C:\Users\ale\FanStacks\Ufficio");
        var b = StackNaming.AppUserModelId(@"C:\Users\ale\FanStacks\Giochi");
        var c = StackNaming.AppUserModelId(@"D:\Altro\Ufficio");
        Assert.NotEqual(a, b);
        Assert.NotEqual(a, c);
    }

    [Theory]
    [InlineData(@"C:\x\Città dei giochi è bella")]
    [InlineData(@"C:\x\!!!")]
    [InlineData(@"C:\x\日本語")]
    public void AppId_HasNoSpacesAndIsShort(string path)
    {
        var id = StackNaming.AppUserModelId(path);
        Assert.DoesNotContain(' ', id);
        Assert.True(id.Length <= 128);
        Assert.All(id, ch => Assert.True(char.IsAsciiLetterOrDigit(ch) || ch == '.'));
    }

    [Fact]
    public void AppId_LongName_IsTruncated()
    {
        var id = StackNaming.AppUserModelId(@"C:\x\" + new string('b', 200));
        Assert.True(id.Length <= 128);
    }

    [Fact]
    public void IconFileName_IsSafe()
    {
        var f = StackNaming.IconFileName(@"C:\x\Città");
        Assert.Matches("^Citta-[0-9a-f]{8}\\.ico$", f);
    }
}

public class CommandLineTests
{
    [Fact]
    public void NoArgs_ShowsHelp() => Assert.IsType<AppCommand.ShowHelp>(CommandLine.Parse([]));

    [Theory]
    [InlineData("--help")]
    [InlineData("-h")]
    [InlineData("/?")]
    public void HelpFlags(string flag) => Assert.IsType<AppCommand.ShowHelp>(CommandLine.Parse([flag]));

    [Fact]
    public void Stack_ParsesPath()
    {
        var cmd = Assert.IsType<AppCommand.OpenStack>(CommandLine.Parse(["--stack", @"C:\Users\ale\FanStacks\Ufficio"]));
        Assert.Equal(@"C:\Users\ale\FanStacks\Ufficio", cmd.FolderPath);
    }

    [Fact]
    public void Stack_IsCaseInsensitive() =>
        Assert.IsType<AppCommand.OpenStack>(CommandLine.Parse(["--STACK", "x"]));

    [Fact]
    public void Create_ParsesNameAndQuiet()
    {
        var cmd = Assert.IsType<AppCommand.CreateStack>(CommandLine.Parse(["--create", "Ufficio", "--quiet"]));
        Assert.Equal("Ufficio", cmd.Name);
        Assert.True(cmd.Quiet);
    }

    [Fact]
    public void RefreshIcons() => Assert.IsType<AppCommand.RefreshIcons>(CommandLine.Parse(["--refresh-icons"]));

    [Theory]
    [InlineData("--stack")]
    [InlineData("--create")]
    [InlineData("--boh")]
    public void InvalidArgs(string arg)
    {
        var cmd = Assert.IsType<AppCommand.Invalid>(CommandLine.Parse([arg]));
        Assert.False(string.IsNullOrWhiteSpace(cmd.Message));
    }

    [Fact]
    public void ExtraArgs_AreInvalid() =>
        Assert.IsType<AppCommand.Invalid>(CommandLine.Parse(["--stack", "a", "b"]));

    [Fact]
    public void StackArguments_QuotesPathAndDropsTrailingBackslash() =>
        // Una barra finale prima delle virgolette verrebbe interpretata come escape.
        Assert.Equal("--stack \"C:\\x\\Ufficio\"", CommandLine.StackArguments(@"C:\x\Ufficio\"));
}

public class IcoWriterTests
{
    [Fact]
    public void WritesValidDirectory()
    {
        var images = new List<(int, byte[])> { (256, new byte[] { 1, 2, 3 }), (32, new byte[] { 4, 5 }), (16, new byte[] { 6 }) };
        using var ms = new MemoryStream();
        IcoWriter.Write(ms, images);
        ms.Position = 0;

        var dir = IcoWriter.ReadDirectory(ms);

        Assert.Equal(new[] { 256, 32, 16 }, dir.Select(d => d.Size));
        Assert.Equal(6 + 16 * 3, dir[0].Offset);
        Assert.Equal(dir[0].Offset + 3, dir[1].Offset);
        var bytes = ms.ToArray();
        Assert.Equal(6 + 16 * 3 + 6, bytes.Length);
        Assert.Equal(4, bytes[dir[1].Offset]);
    }

    [Fact]
    public void EmptyList_Throws() =>
        Assert.Throws<ArgumentException>(() => IcoWriter.Write(new MemoryStream(), []));

    [Fact]
    public void BadSize_Throws() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => IcoWriter.Write(new MemoryStream(), [(300, new byte[1])]));
}

public class CacheKeysTests
{
    private static readonly DateTime T = new(2026, 11, 12, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void IconKey_ChangesWhenFileChanges()
    {
        var k = CacheKeys.IconKey(@"C:\a.lnk", 100, T, 64);
        Assert.Equal(k, CacheKeys.IconKey(@"c:\A.LNK", 100, T, 64));
        Assert.NotEqual(k, CacheKeys.IconKey(@"C:\a.lnk", 101, T, 64));
        Assert.NotEqual(k, CacheKeys.IconKey(@"C:\a.lnk", 100, T.AddSeconds(1), 64));
        Assert.NotEqual(k, CacheKeys.IconKey(@"C:\a.lnk", 100, T, 128));
        Assert.Matches("^[0-9a-f]{16}$", k);
    }

    [Fact]
    public void StackSignature_DependsOnEntriesAndVersion()
    {
        var a = CacheKeys.StackIconSignature([(@"C:\a.lnk", T)], 1);
        Assert.Equal(a, CacheKeys.StackIconSignature([(@"C:\a.lnk", T)], 1));
        Assert.NotEqual(a, CacheKeys.StackIconSignature([(@"C:\a.lnk", T)], 2));
        Assert.NotEqual(a, CacheKeys.StackIconSignature([(@"C:\b.lnk", T)], 1));
        Assert.NotEqual(a, CacheKeys.StackIconSignature([], 1));
    }
}

public class KeyboardNavigatorTests
{
    [Fact]
    public void VerticalFan_UpMovesAwayFromBottomTaskbar()
    {
        var wa = new Rect2(0, 0, 1920, 1032);
        var layout = FanLayoutEngine.Compute(5, new Point2(900, 1032), TaskbarEdge.Bottom, wa);
        var centers = layout.Items.Select(i => i.Center).ToList();
        var start = KeyboardNavigator.Initial(centers, layout.Origin);
        Assert.Equal(0, start);
        Assert.Equal(1, KeyboardNavigator.Next(centers, 0, NavDirection.Up));
        Assert.Equal(0, KeyboardNavigator.Next(centers, 1, NavDirection.Down));
        Assert.Equal(4, KeyboardNavigator.Next(centers, 4, NavDirection.Up)); // fine: resta fermo
        Assert.Equal(0, KeyboardNavigator.Next(centers, 0, NavDirection.Down));
    }

    [Fact]
    public void Grid_MovesByRowsAndColumns()
    {
        var wa = new Rect2(0, 0, 1920, 1032);
        var layout = FanLayoutEngine.Compute(12, new Point2(900, 1032), TaskbarEdge.Bottom, wa);
        var centers = layout.Items.Select(i => i.Center).ToList();
        Assert.Equal(1, KeyboardNavigator.Next(centers, 0, NavDirection.Right));
        Assert.Equal(4, KeyboardNavigator.Next(centers, 0, NavDirection.Down));
        Assert.Equal(0, KeyboardNavigator.Next(centers, 4, NavDirection.Up));
        Assert.Equal(0, KeyboardNavigator.Next(centers, 0, NavDirection.Left));
    }

    [Fact]
    public void Empty_ReturnsMinusOne()
    {
        Assert.Equal(-1, KeyboardNavigator.Initial([], new Point2(0, 0)));
        Assert.Equal(-1, KeyboardNavigator.Next([], 0, NavDirection.Up));
    }
}

public class LogRotationTests
{
    [Theory]
    [InlineData(0, 10, false)]
    [InlineData(100, 10, false)]
    [InlineData(LogRotation.DefaultMaxBytes, 1, true)]
    [InlineData(LogRotation.DefaultMaxBytes - 5, 10, true)]
    public void Rotates_WhenOverLimit(long current, long incoming, bool expected) =>
        Assert.Equal(expected, LogRotation.ShouldRotate(current, incoming));
}
