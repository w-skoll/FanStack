using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace FanStack.App.Interop;

/// <summary>
/// Estrae l'icona di un file (anche .lnk/.url) in alta risoluzione con IShellItemImageFactory.
/// Va chiamato da un thread STA (le estensioni della shell lo richiedono).
/// </summary>
internal static class ShellImage
{
    [Flags]
    private enum SIIGBF : uint
    {
        ResizeToFit = 0x0,
        BiggerSizeOk = 0x1,
        MemoryOnly = 0x2,
        IconOnly = 0x4,
        ThumbnailOnly = 0x8,
        InCacheOnly = 0x10,
    }

    /// <summary>Restituisce un'immagine congelata (utilizzabile da qualsiasi thread) o null.</summary>
    public static BitmapSource? GetIcon(string path, int pixelSize)
    {
        var iid = typeof(IShellItemImageFactory).GUID;
        var hr = SHCreateItemFromParsingName(path, IntPtr.Zero, ref iid, out var factory);
        if (hr < 0 || factory is null)
            return null;

        var hbmp = IntPtr.Zero;
        try
        {
            // IconOnly: niente miniature di contenuto, solo l'icona. Le sovrapposizioni (freccia del
            // collegamento) non sono incluse da GetImage.
            hr = factory.GetImage(new SIZE(pixelSize, pixelSize), SIIGBF.IconOnly | SIIGBF.BiggerSizeOk, out hbmp);
            if (hr < 0 || hbmp == IntPtr.Zero)
                return null;
            return FromHBitmap(hbmp);
        }
        finally
        {
            if (hbmp != IntPtr.Zero)
                NativeMethods.DeleteObject(hbmp);
            Marshal.FinalReleaseComObject(factory);
        }
    }

    /// <summary>
    /// Converte l'HBITMAP (DIB a 32 bit, alfa premoltiplicato) in BitmapSource mantenendo la trasparenza,
    /// cosa che Imaging.CreateBitmapSourceFromHBitmap non fa.
    /// </summary>
    private static BitmapSource? FromHBitmap(IntPtr hbmp)
    {
        var ds = new DIBSECTION();
        if (GetObject(hbmp, Marshal.SizeOf<DIBSECTION>(), ref ds) == 0 || ds.dsBm.bmBits == IntPtr.Zero || ds.dsBm.bmBitsPixel != 32)
            return null;

        var width = ds.dsBm.bmWidth;
        var height = ds.dsBm.bmHeight;
        var stride = ds.dsBm.bmWidthBytes;
        var pixels = new byte[stride * height];
        Marshal.Copy(ds.dsBm.bmBits, pixels, 0, pixels.Length);

        // biHeight positivo = DIB dal basso verso l'alto: ribalta le righe.
        if (ds.dsBmih.biHeight > 0)
        {
            var row = new byte[stride];
            for (var y = 0; y < height / 2; y++)
            {
                var top = y * stride;
                var bottom = (height - 1 - y) * stride;
                Buffer.BlockCopy(pixels, top, row, 0, stride);
                Buffer.BlockCopy(pixels, bottom, pixels, top, stride);
                Buffer.BlockCopy(row, 0, pixels, bottom, stride);
            }
        }

        // La shell restituisce a volte alfa premoltiplicato, a volte no. Se un canale colore supera
        // l'alfa, i dati non possono essere premoltiplicati: allora sono Bgra32 "dritti".
        var hasAlpha = false;
        var premultiplied = true;
        for (var i = 0; i + 3 < pixels.Length; i += 4)
        {
            var a = pixels[i + 3];
            if (a != 0)
                hasAlpha = true;
            if (pixels[i] > a || pixels[i + 1] > a || pixels[i + 2] > a)
                premultiplied = false;
        }
        // Alcune icone vecchie non hanno canale alfa: in quel caso rendile opache.
        if (!hasAlpha)
        {
            for (var i = 3; i < pixels.Length; i += 4)
                pixels[i] = 255;
            premultiplied = true;
        }

        var format = premultiplied ? PixelFormats.Pbgra32 : PixelFormats.Bgra32;
        var bmp = BitmapSource.Create(width, height, 96, 96, format, null, pixels, stride);
        bmp.Freeze();
        return bmp;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
    private static extern int SHCreateItemFromParsingName(string pszPath, IntPtr pbc, ref Guid riid, out IShellItemImageFactory ppv);

    [DllImport("gdi32.dll", EntryPoint = "GetObjectW")]
    private static extern int GetObject(IntPtr hgdiobj, int cbBuffer, ref DIBSECTION lpvObject);

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b")]
    private interface IShellItemImageFactory
    {
        [PreserveSig] int GetImage(SIZE size, SIIGBF flags, out IntPtr phbm);
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct SIZE(int cx, int cy)
    {
        public readonly int cx = cx;
        public readonly int cy = cy;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAP
    {
        public int bmType;
        public int bmWidth;
        public int bmHeight;
        public int bmWidthBytes;
        public ushort bmPlanes;
        public ushort bmBitsPixel;
        public IntPtr bmBits;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFOHEADER
    {
        public uint biSize;
        public int biWidth;
        public int biHeight;
        public ushort biPlanes;
        public ushort biBitCount;
        public uint biCompression;
        public uint biSizeImage;
        public int biXPelsPerMeter;
        public int biYPelsPerMeter;
        public uint biClrUsed;
        public uint biClrImportant;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DIBSECTION
    {
        public BITMAP dsBm;
        public BITMAPINFOHEADER dsBmih;
        public uint dsBitfield0;
        public uint dsBitfield1;
        public uint dsBitfield2;
        public IntPtr dshSection;
        public uint dsOffset;
    }

    /// <summary>Converte in Bgra32 (non premoltiplicato) per salvare in PNG.</summary>
    public static BitmapSource ToBgra32(BitmapSource source)
    {
        if (source.Format == PixelFormats.Bgra32)
            return source;
        var converted = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        converted.Freeze();
        return converted;
    }
}
