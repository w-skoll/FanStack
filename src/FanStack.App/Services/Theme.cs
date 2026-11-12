using System.Windows.Media;
using Microsoft.Win32;

namespace FanStack.App.Services;

/// <summary>Colori del ventaglio in base al tema chiaro/scuro di Windows.</summary>
internal sealed record Theme(
    bool IsDark,
    Color PillBackground,
    Color PillBorder,
    Color PillText,
    Color PopupBackground,
    Color PopupBorder,
    Color GridText,
    Color SelectionPlate,
    Color Accent)
{
    public static Theme Current()
    {
        var dark = ReadDword(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "AppsUseLightTheme") is 0;
        var accent = ReadAccent() ?? Color.FromRgb(0x4C, 0x8D, 0xFF);
        return dark
            ? new Theme(true,
                PillBackground: Color.FromArgb(0xE0, 0x1E, 0x1F, 0x24),
                PillBorder: Color.FromArgb(0x38, 0xFF, 0xFF, 0xFF),
                PillText: Color.FromRgb(0xF4, 0xF5, 0xF7),
                PopupBackground: Color.FromArgb(0xEE, 0x22, 0x24, 0x2A),
                PopupBorder: Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF),
                GridText: Color.FromRgb(0xEE, 0xEF, 0xF2),
                SelectionPlate: Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF),
                Accent: accent)
            : new Theme(false,
                PillBackground: Color.FromArgb(0xE6, 0xF7, 0xF7, 0xF9),
                PillBorder: Color.FromArgb(0x30, 0x00, 0x00, 0x00),
                PillText: Color.FromRgb(0x1C, 0x1D, 0x21),
                PopupBackground: Color.FromArgb(0xF2, 0xF5, 0xF6, 0xF8),
                PopupBorder: Color.FromArgb(0x30, 0x00, 0x00, 0x00),
                GridText: Color.FromRgb(0x1C, 0x1D, 0x21),
                SelectionPlate: Color.FromArgb(0x30, 0x00, 0x00, 0x00),
                Accent: accent);
    }

    private static int? ReadDword(string key, string name)
    {
        try
        {
            using var k = Registry.CurrentUser.OpenSubKey(key);
            return k?.GetValue(name) is int v ? v : null;
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            Log.Error($"Registro non leggibile: {key}\\{name}", ex);
            return null;
        }
    }

    /// <summary>Colore d'accento di Windows (DWORD in formato ABGR).</summary>
    private static Color? ReadAccent()
    {
        var v = ReadDword(@"Software\Microsoft\Windows\DWM", "AccentColor");
        if (v is not { } abgr)
            return null;
        var u = unchecked((uint)abgr);
        return Color.FromRgb((byte)(u & 0xFF), (byte)((u >> 8) & 0xFF), (byte)((u >> 16) & 0xFF));
    }
}
