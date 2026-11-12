using FanStack.Core.Geometry;

namespace FanStack.Core.Tests;

public class FanLayoutEngineTests
{
    private static readonly FanLayoutOptions Opt = new();

    /// <summary>Monitor di prova: (nome, monitor, area di lavoro con taskbar sul bordo indicato).</summary>
    public static IEnumerable<object[]> Scenarios()
    {
        var screens = new (string Name, Rect2 Monitor)[]
        {
            ("FullHD", new Rect2(0, 0, 1920, 1080)),
            ("Laptop", new Rect2(0, 0, 1366, 768)),
            ("Piccolo", new Rect2(0, 0, 800, 600)),
            ("SecondoMonitorSinistra", new Rect2(-2560, -200, 2560, 1440)),
        };
        const double bar = 48;
        foreach (var (name, m) in screens)
        {
            foreach (var edge in Enum.GetValues<TaskbarEdge>())
            {
                var wa = edge switch
                {
                    TaskbarEdge.Bottom => new Rect2(m.X, m.Y, m.Width, m.Height - bar),
                    TaskbarEdge.Top => new Rect2(m.X, m.Y + bar, m.Width, m.Height - bar),
                    TaskbarEdge.Left => new Rect2(m.X + bar, m.Y, m.Width - bar, m.Height),
                    _ => new Rect2(m.X, m.Y, m.Width - bar, m.Height),
                };
                // Cursori: centro, vicino ai due angoli lungo la taskbar.
                var cursors = new[]
                {
                    m.Center,
                    new Point2(m.Left + 3, m.Top + 3),
                    new Point2(m.Right - 3, m.Bottom - 3),
                    new Point2(m.Left + 3, m.Bottom - 3),
                    new Point2(m.Right - 3, m.Top + 3),
                };
                foreach (var c in cursors)
                    yield return new object[] { name, m, wa, edge, c };
            }
        }
    }

    [Theory]
    [MemberData(nameof(Scenarios))]
    public void AllCounts_StayInsideWorkArea_AndOpenInward(string name, Rect2 monitor, Rect2 workArea, TaskbarEdge edge, Point2 cursor)
    {
        var placement = TaskbarLocator.Resolve(monitor, workArea, cursor);
        Assert.Equal(edge, placement.Edge);

        for (var n = 1; n <= 30; n++)
        {
            var layout = FanLayoutEngine.Compute(n, placement.Origin, placement.Edge, placement.WorkArea, Opt);
            var ctx = $"{name} {edge} n={n} cursor={cursor}";

            Assert.True(n == layout.Items.Count, ctx);
            Assert.Equal(Enumerable.Range(0, n), layout.Items.Select(i => i.Index));
            Assert.True(workArea.Contains(layout.Bounds), $"{ctx}: bounds {layout.Bounds} fuori da {workArea}");

            if (n > Opt.MaxFanItems)
                Assert.True(layout.Mode == LayoutMode.Grid, ctx);

            foreach (var item in layout.Items)
            {
                Assert.True(item.DelayMs >= 0, ctx);
                Assert.True(Math.Abs(item.RotationDegrees) <= Opt.RotationMax + 0.001, ctx);
            }

            if (layout.Mode == LayoutMode.Fan)
            {
                foreach (var item in layout.Items)
                {
                    Assert.True(workArea.Contains(item.IconRect), $"{ctx}: icona {item.Index} {item.IconRect} fuori");
                    Assert.True(workArea.Contains(item.LabelRect), $"{ctx}: etichetta {item.Index} {item.LabelRect} fuori");
                    Assert.True(layout.Bounds.Contains(item.IconRect), ctx);
                    AssertInward(edge, placement.Origin, item.IconRect, ctx);
                }
                AssertNoOverlap(layout.Items.Select(i => i.IconRect).ToList(), ctx);
            }
            else
            {
                var grid = layout.Grid!;
                Assert.True(workArea.Contains(grid.Popup), $"{ctx}: griglia {grid.Popup} fuori");
                Assert.True(layout.Bounds.Contains(grid.Popup), ctx);
                Assert.True(grid.Columns * grid.Rows >= n, ctx);
                AssertInward(edge, placement.Origin, grid.Popup, ctx);
                if (!grid.NeedsScroll)
                    Assert.All(layout.Items, i => Assert.True(grid.Popup.Contains(i.IconRect), ctx));
                AssertNoOverlap(layout.Items.Select(i => i.IconRect).ToList(), ctx);
            }
        }
    }

    [Theory]
    [InlineData(TaskbarEdge.Bottom)]
    [InlineData(TaskbarEdge.Top)]
    [InlineData(TaskbarEdge.Left)]
    [InlineData(TaskbarEdge.Right)]
    public void UpToTen_OnFullHd_IsFan(TaskbarEdge edge)
    {
        var (wa, origin) = FullHd(edge);
        for (var n = 1; n <= 10; n++)
            Assert.Equal(LayoutMode.Fan, FanLayoutEngine.Compute(n, origin, edge, wa).Mode);
        Assert.Equal(LayoutMode.Grid, FanLayoutEngine.Compute(11, origin, edge, wa).Mode);
    }

    [Fact]
    public void Bottom_FirstItemIsNearestTaskbar_AndCurveBendsRight()
    {
        var (wa, origin) = FullHd(TaskbarEdge.Bottom);
        var layout = FanLayoutEngine.Compute(8, origin, TaskbarEdge.Bottom, wa);
        var ys = layout.Items.Select(i => i.Center.Y).ToList();
        Assert.Equal(ys.OrderByDescending(y => y), ys);
        Assert.True(layout.Items[^1].Center.X > layout.Items[0].Center.X);
        Assert.True(layout.Items[^1].RotationDegrees > 0);
        Assert.Equal(0, layout.Items[0].RotationDegrees, 3);
        Assert.All(layout.Items, i => Assert.Equal(LabelSide.Left, i.LabelSide));
        Assert.Equal(origin.X, layout.Items[0].Center.X, 3);
    }

    [Fact]
    public void Top_GrowsDownward()
    {
        var (wa, origin) = FullHd(TaskbarEdge.Top);
        var layout = FanLayoutEngine.Compute(5, origin, TaskbarEdge.Top, wa);
        var ys = layout.Items.Select(i => i.Center.Y).ToList();
        Assert.Equal(ys.OrderBy(y => y), ys);
        Assert.True(layout.Items[^1].RotationDegrees < 0);
    }

    [Fact]
    public void NearLeftScreenEdge_MirrorsLabelsToTheRight()
    {
        var wa = new Rect2(0, 0, 1920, 1032);
        var layout = FanLayoutEngine.Compute(6, new Point2(20, 1032), TaskbarEdge.Bottom, wa);
        Assert.All(layout.Items, i => Assert.Equal(LabelSide.Right, i.LabelSide));
        Assert.All(layout.Items, i => Assert.True(wa.Contains(i.LabelRect)));
    }

    [Fact]
    public void SideFan_IsCenteredOnCursor_WithLabelsInward()
    {
        var wa = new Rect2(48, 0, 1872, 1080);
        var layout = FanLayoutEngine.Compute(5, new Point2(48, 500), TaskbarEdge.Left, wa);
        Assert.Equal(500, layout.Items[2].Center.Y, 3);
        Assert.All(layout.Items, i => Assert.Equal(LabelSide.Right, i.LabelSide));
        // Arco: l'icona centrale è la più lontana dalla taskbar.
        Assert.Equal(layout.Items.Max(i => i.Center.X), layout.Items[2].Center.X, 3);
        // Ritardo: parte dal centro.
        Assert.Equal(0, layout.Items[2].DelayMs);
    }

    [Fact]
    public void Zero_ReturnsEmptyLayout()
    {
        var (wa, origin) = FullHd(TaskbarEdge.Bottom);
        var layout = FanLayoutEngine.Compute(0, origin, TaskbarEdge.Bottom, wa);
        Assert.Empty(layout.Items);
    }

    [Fact]
    public void Negative_Throws()
    {
        var (wa, origin) = FullHd(TaskbarEdge.Bottom);
        Assert.Throws<ArgumentOutOfRangeException>(() => FanLayoutEngine.Compute(-1, origin, TaskbarEdge.Bottom, wa));
    }

    [Fact]
    public void ManyItems_OnSmallScreen_GridScrolls()
    {
        var wa = new Rect2(0, 0, 800, 552);
        var layout = FanLayoutEngine.Compute(30, new Point2(400, 552), TaskbarEdge.Bottom, wa);
        Assert.Equal(LayoutMode.Grid, layout.Mode);
        Assert.True(layout.Grid!.NeedsScroll);
        Assert.True(wa.Contains(layout.Grid.Popup));
    }

    [Fact]
    public void FanTooTallForScreen_FallsBackToGrid()
    {
        var wa = new Rect2(0, 0, 1024, 400);
        var layout = FanLayoutEngine.Compute(9, new Point2(500, 400), TaskbarEdge.Bottom, wa);
        Assert.Equal(LayoutMode.Grid, layout.Mode);
    }

    private static (Rect2 Wa, Point2 Origin) FullHd(TaskbarEdge edge)
    {
        var m = new Rect2(0, 0, 1920, 1080);
        var wa = edge switch
        {
            TaskbarEdge.Bottom => new Rect2(0, 0, 1920, 1032),
            TaskbarEdge.Top => new Rect2(0, 48, 1920, 1032),
            TaskbarEdge.Left => new Rect2(48, 0, 1872, 1080),
            _ => new Rect2(0, 0, 1872, 1080),
        };
        return (wa, TaskbarLocator.Resolve(m, wa, m.Center).Origin);
    }

    private static void AssertInward(TaskbarEdge edge, Point2 origin, Rect2 r, string ctx)
    {
        var ok = edge switch
        {
            TaskbarEdge.Bottom => r.Bottom <= origin.Y + 0.001,
            TaskbarEdge.Top => r.Top >= origin.Y - 0.001,
            TaskbarEdge.Left => r.Left >= origin.X - 0.001,
            _ => r.Right <= origin.X + 0.001,
        };
        Assert.True(ok, $"{ctx}: {r} non è verso l'interno rispetto a {origin}");
    }

    private static void AssertNoOverlap(IReadOnlyList<Rect2> rects, string ctx)
    {
        for (var a = 0; a < rects.Count; a++)
            for (var b = a + 1; b < rects.Count; b++)
                Assert.False(rects[a].Intersects(rects[b]), $"{ctx}: icone {a} e {b} sovrapposte");
    }
}

public class TaskbarLocatorTests
{
    private static readonly Rect2 Monitor = new(0, 0, 1920, 1080);

    [Theory]
    [InlineData(0, 0, 1920, 1032, TaskbarEdge.Bottom)]
    [InlineData(0, 48, 1920, 1032, TaskbarEdge.Top)]
    [InlineData(48, 0, 1872, 1080, TaskbarEdge.Left)]
    [InlineData(0, 0, 1872, 1080, TaskbarEdge.Right)]
    public void DetectsEdgeFromWorkAreaInset(double x, double y, double w, double h, TaskbarEdge expected)
    {
        var p = TaskbarLocator.Resolve(Monitor, new Rect2(x, y, w, h), new Point2(900, 1060));
        Assert.Equal(expected, p.Edge);
        Assert.False(p.AutoHide);
    }

    [Fact]
    public void SnapsOriginToTaskbarEdge()
    {
        var p = TaskbarLocator.Resolve(Monitor, new Rect2(0, 0, 1920, 1032), new Point2(700, 1060));
        Assert.Equal(new Point2(700, 1032), p.Origin);
    }

    [Fact]
    public void AutoHide_UsesHintAndReservesSpace()
    {
        var p = TaskbarLocator.Resolve(Monitor, Monitor, new Point2(700, 1075), TaskbarEdge.Bottom);
        Assert.True(p.AutoHide);
        Assert.Equal(TaskbarEdge.Bottom, p.Edge);
        Assert.Equal(1080 - TaskbarLocator.AutoHideReserve, p.Origin.Y);
    }

    [Theory]
    [InlineData(5, 500, TaskbarEdge.Left)]
    [InlineData(1915, 500, TaskbarEdge.Right)]
    [InlineData(900, 3, TaskbarEdge.Top)]
    [InlineData(900, 1078, TaskbarEdge.Bottom)]
    public void AutoHide_WithoutHint_UsesNearestEdge(double cx, double cy, TaskbarEdge expected)
    {
        var p = TaskbarLocator.Resolve(Monitor, Monitor, new Point2(cx, cy));
        Assert.Equal(expected, p.Edge);
    }

    [Fact]
    public void SecondMonitorWithNegativeCoordinates()
    {
        var m = new Rect2(-1920, 0, 1920, 1080);
        var p = TaskbarLocator.Resolve(m, new Rect2(-1920, 0, 1920, 1032), new Point2(-100, 1050));
        Assert.Equal(TaskbarEdge.Bottom, p.Edge);
        Assert.Equal(new Point2(-100, 1032), p.Origin);
    }
}
