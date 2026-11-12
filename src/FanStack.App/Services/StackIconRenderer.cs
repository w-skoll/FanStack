using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using FanStack.Core.Imaging;

namespace FanStack.App.Services;

/// <summary>
/// Genera l'icona .ico di uno stack: riquadro arrotondato scuro con l'anteprima delle prime 4 app.
/// Grafica tutta generata in codice. Da chiamare su un thread STA.
/// </summary>
internal static class StackIconRenderer
{
    /// <summary>Aumentare quando cambia il disegno: forza la rigenerazione delle icone.</summary>
    public const int Version = 1;

    private static readonly int[] Sizes = [256, 64, 48, 40, 32, 24, 20, 16];

    public static byte[] RenderIco(IReadOnlyList<BitmapSource> previews)
    {
        var images = Sizes.Select(s => (s, EncodePng(Render(previews, s)))).ToList();
        using var ms = new MemoryStream();
        IcoWriter.Write(ms, images);
        return ms.ToArray();
    }

    public static BitmapSource Render(IReadOnlyList<BitmapSource> previews, int size)
    {
        var s = (double)size;
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            // Riquadro: vetro scuro con bordo chiaro sottile.
            var inset = Math.Max(0.5, s * 0.04);
            var tile = new Rect(inset, inset, s - 2 * inset, s - 2 * inset);
            var radius = s * 0.22;
            var bg = new LinearGradientBrush(Color.FromRgb(0x4A, 0x50, 0x5E), Color.FromRgb(0x23, 0x27, 0x30), 90);
            var border = new Pen(new SolidColorBrush(Color.FromArgb(0x55, 0xFF, 0xFF, 0xFF)), Math.Max(1, s / 96));
            dc.DrawRoundedRectangle(bg, border, tile, radius, radius);
            var gloss = new LinearGradientBrush(Color.FromArgb(0x30, 0xFF, 0xFF, 0xFF), Color.FromArgb(0x00, 0xFF, 0xFF, 0xFF), 90);
            dc.DrawRoundedRectangle(gloss, null, new Rect(tile.X, tile.Y, tile.Width, tile.Height * 0.55), radius, radius);

            if (previews.Count == 0)
                DrawEmptyFan(dc, tile);
            else if (size <= 16)
                DrawImage(dc, previews[0], Inset(tile, tile.Width * 0.14));
            else
                DrawGrid(dc, previews.Take(4).ToList(), tile);
        }

        var rtb = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(visual);
        rtb.Freeze();
        return rtb;
    }

    private static void DrawGrid(DrawingContext dc, IReadOnlyList<BitmapSource> icons, Rect tile)
    {
        var pad = tile.Width * 0.13;
        var gap = tile.Width * 0.06;
        var cell = (tile.Width - 2 * pad - gap) / 2;

        if (icons.Count == 1)
        {
            DrawImage(dc, icons[0], Inset(tile, tile.Width * 0.18));
            return;
        }

        for (var i = 0; i < icons.Count; i++)
        {
            var col = i % 2;
            var row = i / 2;
            var x = tile.X + pad + col * (cell + gap);
            // Con 2 o 3 icone le righe incomplete vengono centrate.
            var rowCount = Math.Min(2, icons.Count - row * 2);
            if (rowCount == 1)
                x = tile.X + (tile.Width - cell) / 2;
            var rows = (icons.Count + 1) / 2;
            var yStart = rows == 1 ? tile.Y + (tile.Height - cell) / 2 : tile.Y + pad;
            var y = yStart + row * (cell + gap);
            DrawImage(dc, icons[i], new Rect(x, y, cell, cell));
        }
    }

    /// <summary>Stack vuoto: tre schede a ventaglio (disegno originale).</summary>
    private static void DrawEmptyFan(DrawingContext dc, Rect tile)
    {
        var c = new Point(tile.X + tile.Width / 2, tile.Y + tile.Height * 0.78);
        var w = tile.Width * 0.30;
        var h = tile.Height * 0.46;
        var colors = new[]
        {
            Color.FromRgb(0x4F, 0x7C, 0xFF),
            Color.FromRgb(0x2E, 0xC4, 0x9A),
            Color.FromRgb(0xFF, 0x8A, 0x4C),
        };
        var angles = new[] { -22.0, 0.0, 22.0 };
        for (var i = 0; i < 3; i++)
        {
            dc.PushTransform(new RotateTransform(angles[i], c.X, c.Y));
            var r = new Rect(c.X - w / 2, c.Y - h, w, h);
            dc.DrawRoundedRectangle(new SolidColorBrush(colors[i]), new Pen(new SolidColorBrush(Color.FromArgb(0x60, 0xFF, 0xFF, 0xFF)), Math.Max(0.5, tile.Width / 128)), r, w * 0.22, w * 0.22);
            dc.Pop();
        }
    }

    private static void DrawImage(DrawingContext dc, BitmapSource img, Rect r)
    {
        var group = new DrawingGroup();
        RenderOptions.SetBitmapScalingMode(group, BitmapScalingMode.HighQuality);
        group.Children.Add(new ImageDrawing(img, r));
        dc.DrawDrawing(group);
    }

    private static Rect Inset(Rect r, double d) => new(r.X + d, r.Y + d, Math.Max(1, r.Width - 2 * d), Math.Max(1, r.Height - 2 * d));

    private static byte[] EncodePng(BitmapSource bmp)
    {
        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(Interop.ShellImage.ToBgra32(bmp)));
        using var ms = new MemoryStream();
        enc.Save(ms);
        return ms.ToArray();
    }
}
