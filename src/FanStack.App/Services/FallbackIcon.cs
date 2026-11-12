using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using FanStack.Core;

namespace FanStack.App.Services;

/// <summary>Icona di riserva generata in codice: riquadro arrotondato colorato con l'iniziale.</summary>
internal static class FallbackIcon
{
    private static readonly (Color A, Color B)[] Palette =
    [
        (Color.FromRgb(0x4F, 0x7C, 0xFF), Color.FromRgb(0x2B, 0x4F, 0xD8)),
        (Color.FromRgb(0x2E, 0xC4, 0x9A), Color.FromRgb(0x14, 0x8F, 0x73)),
        (Color.FromRgb(0xFF, 0x8A, 0x4C), Color.FromRgb(0xE0, 0x5A, 0x2B)),
        (Color.FromRgb(0xB0, 0x6C, 0xFF), Color.FromRgb(0x7A, 0x3F, 0xDB)),
        (Color.FromRgb(0xFF, 0x5F, 0x7E), Color.FromRgb(0xD6, 0x35, 0x5A)),
        (Color.FromRgb(0x3C, 0xB4, 0xE6), Color.FromRgb(0x1F, 0x7F, 0xB8)),
    ];

    public static BitmapSource Create(string name, int pixelSize)
    {
        var (a, b) = Palette[Hashing.Fnv1a32(name) % Palette.Length];
        var letter = string.IsNullOrWhiteSpace(name) ? "?" : name.Trim()[..1].ToUpper(CultureInfo.CurrentCulture);
        var s = (double)pixelSize;

        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            var inset = s * 0.06;
            var rect = new Rect(inset, inset, s - 2 * inset, s - 2 * inset);
            var brush = new LinearGradientBrush(a, b, 90);
            dc.DrawRoundedRectangle(brush, null, rect, s * 0.22, s * 0.22);
            // Riflesso superiore morbido.
            var gloss = new LinearGradientBrush(Color.FromArgb(70, 255, 255, 255), Color.FromArgb(0, 255, 255, 255), 90);
            dc.DrawRoundedRectangle(gloss, null, new Rect(rect.X, rect.Y, rect.Width, rect.Height * 0.5), s * 0.22, s * 0.22);

            var text = new FormattedText(letter, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal),
                s * 0.5, Brushes.White, 1.0);
            dc.DrawText(text, new Point((s - text.Width) / 2, (s - text.Height) / 2));
        }

        var rtb = new RenderTargetBitmap(pixelSize, pixelSize, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(visual);
        rtb.Freeze();
        return rtb;
    }
}
