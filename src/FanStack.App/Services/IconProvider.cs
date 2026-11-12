using System.Windows.Media.Imaging;
using FanStack.App.Interop;
using FanStack.Core;

namespace FanStack.App.Services;

/// <summary>
/// Icone delle app: prima la cache su disco (PNG), altrimenti estrazione dalla shell e salvataggio.
/// La chiave della cache include dimensione e data di modifica del collegamento, quindi
/// un collegamento modificato rigenera l'icona da solo. Se tutto fallisce: icona di riserva.
/// Da chiamare sul thread STA di <see cref="StaWorker"/>.
/// </summary>
internal static class IconProvider
{
    /// <summary>Dimensione in pixel da chiedere alla shell, arrotondata a misure standard.</summary>
    public static int PixelSizeFor(double dipSize, double scale)
    {
        var needed = dipSize * scale * 1.15; // margine per l'ingrandimento al passaggio del mouse
        return needed <= 48 ? 48 : needed <= 64 ? 64 : needed <= 96 ? 96 : needed <= 128 ? 128 : 256;
    }

    public static BitmapSource Get(string path, string displayName, int pixelSize)
    {
        string? cacheFile = null;
        try
        {
            var info = new FileInfo(path);
            if (info.Exists)
            {
                var key = CacheKeys.IconKey(info.FullName, info.Length, info.LastWriteTimeUtc, pixelSize);
                cacheFile = Path.Combine(AppPaths.IconCacheDir, key + ".png");
                if (File.Exists(cacheFile))
                {
                    var cached = LoadPng(cacheFile);
                    if (cached is not null)
                        return cached;
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            Log.Error($"Cache icona non leggibile per {path}", ex);
        }

        BitmapSource? icon = null;
        try
        {
            icon = ShellImage.GetIcon(path, pixelSize);
        }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or InvalidCastException or ArgumentException)
        {
            Log.Error($"Estrazione icona fallita per {path}", ex);
        }

        if (icon is null)
            return FallbackIcon.Create(displayName, pixelSize);

        if (cacheFile is not null)
            SavePng(icon, cacheFile);
        return icon;
    }

    private static BitmapSource? LoadPng(string file)
    {
        try
        {
            using var fs = File.OpenRead(file);
            var decoder = new PngBitmapDecoder(fs, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            var frame = decoder.Frames[0];
            frame.Freeze();
            return frame;
        }
        catch (Exception ex) when (ex is IOException or NotSupportedException or FileFormatException or InvalidOperationException)
        {
            Log.Error($"PNG in cache corrotto, lo rigenero: {file}", ex);
            return null;
        }
    }

    private static void SavePng(BitmapSource icon, string file)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(ShellImage.ToBgra32(icon)));
            var tmp = file + ".tmp";
            using (var fs = File.Create(tmp))
                encoder.Save(fs);
            File.Move(tmp, file, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Error($"Impossibile salvare l'icona in cache: {file}", ex);
        }
    }
}
