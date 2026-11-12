namespace FanStack.Core.Imaging;

/// <summary>Scrive un file .ico con immagini PNG (formato supportato da Windows Vista in poi).</summary>
public static class IcoWriter
{
    public static void Write(Stream output, IReadOnlyList<(int Size, byte[] Png)> images)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(images);
        if (images.Count == 0)
            throw new ArgumentException("Serve almeno un'immagine.", nameof(images));

        using var w = new BinaryWriter(output, System.Text.Encoding.UTF8, leaveOpen: true);
        // ICONDIR
        w.Write((ushort)0); // riservato
        w.Write((ushort)1); // tipo: icona
        w.Write((ushort)images.Count);

        var offset = 6 + 16 * images.Count;
        foreach (var (size, png) in images)
        {
            if (size is < 1 or > 256)
                throw new ArgumentOutOfRangeException(nameof(images), size, "Dimensione icona fuori intervallo (1-256).");
            // ICONDIRENTRY: 256 si scrive come 0.
            w.Write((byte)(size == 256 ? 0 : size));
            w.Write((byte)(size == 256 ? 0 : size));
            w.Write((byte)0);   // colori in tavolozza
            w.Write((byte)0);   // riservato
            w.Write((ushort)1); // piani
            w.Write((ushort)32); // bit per pixel
            w.Write((uint)png.Length);
            w.Write((uint)offset);
            offset += png.Length;
        }
        foreach (var (_, png) in images)
            w.Write(png);
    }

    /// <summary>Legge l'intestazione di un .ico (usato nei test e per controlli).</summary>
    public static IReadOnlyList<(int Size, int Length, int Offset)> ReadDirectory(Stream input)
    {
        using var r = new BinaryReader(input, System.Text.Encoding.UTF8, leaveOpen: true);
        if (r.ReadUInt16() != 0 || r.ReadUInt16() != 1)
            throw new InvalidDataException("Non è un file .ico.");
        var count = r.ReadUInt16();
        var list = new List<(int, int, int)>(count);
        for (var i = 0; i < count; i++)
        {
            int width = r.ReadByte();
            r.ReadByte(); r.ReadByte(); r.ReadByte();
            r.ReadUInt16(); r.ReadUInt16();
            var len = (int)r.ReadUInt32();
            var off = (int)r.ReadUInt32();
            list.Add((width == 0 ? 256 : width, len, off));
        }
        return list;
    }
}
