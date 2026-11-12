namespace FanStack.Core.Geometry;

/// <summary>Disposizione scelta: ventaglio o griglia.</summary>
public enum LayoutMode
{
    Fan,
    Grid,
}

/// <summary>Lato dell'icona su cui sta l'etichetta.</summary>
public enum LabelSide
{
    Left,
    Right,
    Below,
}

/// <summary>Parametri estetici del layout (in DIP).</summary>
public sealed record FanLayoutOptions
{
    public double IconSize { get; init; } = 56;
    /// <summary>Spazio tra un'icona e la successiva nel ventaglio.</summary>
    public double Spacing { get; init; } = 14;
    /// <summary>Distanza della prima icona dal bordo della taskbar.</summary>
    public double EdgeGap { get; init; } = 14;
    /// <summary>Larghezza massima riservata all'etichetta.</summary>
    public double LabelWidth { get; init; } = 170;
    public double LabelHeight { get; init; } = 26;
    public double LabelGap { get; init; } = 10;
    /// <summary>Margine minimo dai bordi dell'area di lavoro.</summary>
    public double Margin { get; init; } = 8;
    /// <summary>Spazio extra attorno al contenuto per ombre e ingrandimento al passaggio del mouse.</summary>
    public double ShadowPadding { get; init; } = 18;
    /// <summary>Oltre questo numero di elementi si passa alla griglia.</summary>
    public int MaxFanItems { get; init; } = 10;
    /// <summary>Spostamento laterale massimo della curva (con ventaglio pieno).</summary>
    public double BowMax { get; init; } = 64;
    /// <summary>Curvatura dei ventagli laterali (taskbar a sinistra o destra).</summary>
    public double SideBow { get; init; } = 22;
    public double RotationMax { get; init; } = 7;
    public int StaggerMs { get; init; } = 22;
    public double GridPadding { get; init; } = 18;
    public double GridCellExtraWidth { get; init; } = 44;
    /// <summary>Altezza massima della griglia rispetto all'area di lavoro.</summary>
    public double GridMaxHeightRatio { get; init; } = 0.8;

    public double Step => IconSize + Spacing;
}

/// <summary>Posizione calcolata di un'icona.</summary>
/// <param name="Index">Indice della voce nello stack.</param>
/// <param name="Center">Centro dell'icona.</param>
/// <param name="RotationDegrees">Rotazione (positivo = orario).</param>
/// <param name="DelayMs">Ritardo dell'animazione di apertura.</param>
/// <param name="IconRect">Rettangolo dell'icona (senza rotazione).</param>
/// <param name="LabelRect">Rettangolo massimo riservato all'etichetta.</param>
/// <param name="LabelSide">Lato dell'etichetta.</param>
public sealed record FanItem(int Index, Point2 Center, double RotationDegrees, int DelayMs, Rect2 IconRect, Rect2 LabelRect, LabelSide LabelSide);

/// <summary>Dati della griglia (solo in modalità griglia).</summary>
public sealed record GridInfo(Rect2 Popup, int Columns, int Rows, double CellWidth, double CellHeight, double ContentHeight, bool NeedsScroll, double Padding);

/// <summary>Risultato del calcolo: posizioni assolute nelle stesse unità dell'area di lavoro.</summary>
/// <param name="Bounds">Rettangolo che contiene tutto (con spazio per le ombre), dentro l'area di lavoro: è la finestra.</param>
public sealed record FanLayout(LayoutMode Mode, TaskbarEdge Edge, Point2 Origin, IReadOnlyList<FanItem> Items, Rect2 Bounds, GridInfo? Grid);

/// <summary>Calcola la disposizione del ventaglio. Logica pura, testata in FanStack.Core.Tests.</summary>
public static class FanLayoutEngine
{
    public static FanLayout Compute(int count, Point2 origin, TaskbarEdge edge, Rect2 workArea, FanLayoutOptions? options = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        var o = options ?? new FanLayoutOptions();
        if (workArea.IsEmpty)
            throw new ArgumentException("Area di lavoro vuota.", nameof(workArea));

        origin = new Point2(Math.Clamp(origin.X, workArea.Left, workArea.Right), Math.Clamp(origin.Y, workArea.Top, workArea.Bottom));

        if (count == 0)
            return new FanLayout(LayoutMode.Fan, edge, origin, Array.Empty<FanItem>(), new Rect2(origin.X, origin.Y, 0, 0), null);

        if (count <= o.MaxFanItems)
        {
            var fan = TryFan(count, origin, edge, workArea, o);
            if (fan is not null)
                return fan;
        }
        return Grid(count, origin, edge, workArea, o);
    }

    private static FanLayout? TryFan(int n, Point2 origin, TaskbarEdge edge, Rect2 wa, FanLayoutOptions o)
    {
        var inner = wa.Inflate(-o.Margin);
        var items = edge is TaskbarEdge.Bottom or TaskbarEdge.Top
            ? VerticalFan(n, origin, edge, inner, o)
            : SideFan(n, origin, edge, o);

        // Clamp: sposta tutto il ventaglio dentro l'area visibile.
        var content = items.Select(i => i.IconRect.Union(i.LabelRect)).Aggregate((a, b) => a.Union(b));
        if (content.Width > inner.Width || content.Height > inner.Height)
            return null;

        var dx = ShiftInto(content.Left, content.Right, inner.Left, inner.Right);
        var dy = ShiftInto(content.Top, content.Bottom, inner.Top, inner.Bottom);
        if (dx != 0 || dy != 0)
        {
            items = items.Select(i => i with
            {
                Center = i.Center.Offset(dx, dy),
                IconRect = i.IconRect.Offset(dx, dy),
                LabelRect = i.LabelRect.Offset(dx, dy),
            }).ToList();
            content = content.Offset(dx, dy);
        }

        var bounds = content.Inflate(o.ShadowPadding).Intersect(wa);
        return new FanLayout(LayoutMode.Fan, edge, origin, items, bounds, null);
    }

    /// <summary>Taskbar in basso o in alto: colonna che sale (o scende) con una leggera curva laterale.</summary>
    private static List<FanItem> VerticalFan(int n, Point2 origin, TaskbarEdge edge, Rect2 inner, FanLayoutOptions o)
    {
        var step = o.Step;
        var refLen = Math.Max(1, (o.MaxFanItems - 1) * step);
        var half = o.IconSize / 2;

        // Etichette a sinistra e curva verso destra; se a sinistra non c'è spazio si specchia tutto.
        var labelsLeft = origin.X - half - o.LabelGap - o.LabelWidth >= inner.Left;
        var bowSign = labelsLeft ? 1 : -1;
        var dirY = edge == TaskbarEdge.Bottom ? -1 : 1;
        // In basso la colonna sale curvando: rotazione oraria se la curva va a destra.
        var rotSign = bowSign * (edge == TaskbarEdge.Bottom ? 1 : -1);

        var items = new List<FanItem>(n);
        for (var i = 0; i < n; i++)
        {
            var along = i * step;
            var frac = along / refLen;
            var cx = origin.X + bowSign * o.BowMax * frac * frac;
            var cy = origin.Y + dirY * (o.EdgeGap + half + along);
            var center = new Point2(cx, cy);
            var icon = Rect2.FromCenter(center, o.IconSize, o.IconSize);
            var label = labelsLeft
                ? new Rect2(icon.Left - o.LabelGap - o.LabelWidth, cy - o.LabelHeight / 2, o.LabelWidth, o.LabelHeight)
                : new Rect2(icon.Right + o.LabelGap, cy - o.LabelHeight / 2, o.LabelWidth, o.LabelHeight);
            items.Add(new FanItem(i, center, rotSign * o.RotationMax * frac, i * o.StaggerMs, icon, label,
                labelsLeft ? LabelSide.Left : LabelSide.Right));
        }
        return items;
    }

    /// <summary>Taskbar a sinistra o a destra: colonna centrata sul cursore, ad arco verso l'interno.</summary>
    private static List<FanItem> SideFan(int n, Point2 origin, TaskbarEdge edge, FanLayoutOptions o)
    {
        var step = o.Step;
        var half = o.IconSize / 2;
        var mid = (n - 1) / 2.0;
        var inward = edge == TaskbarEdge.Left ? 1 : -1;

        var items = new List<FanItem>(n);
        for (var i = 0; i < n; i++)
        {
            var frac = mid == 0 ? 0 : (i - mid) / mid; // da -1 (in alto) a 1 (in basso)
            var bow = o.SideBow * (1 - frac * frac);
            var cx = origin.X + inward * (o.EdgeGap + half + bow);
            var cy = origin.Y + (i - mid) * step;
            var center = new Point2(cx, cy);
            var icon = Rect2.FromCenter(center, o.IconSize, o.IconSize);
            var label = edge == TaskbarEdge.Left
                ? new Rect2(icon.Right + o.LabelGap, cy - o.LabelHeight / 2, o.LabelWidth, o.LabelHeight)
                : new Rect2(icon.Left - o.LabelGap - o.LabelWidth, cy - o.LabelHeight / 2, o.LabelWidth, o.LabelHeight);
            // Le icone seguono l'arco: in alto ruotano verso l'interno, in basso verso l'esterno.
            var rotation = -inward * o.RotationMax * 0.6 * frac;
            var delay = (int)Math.Round(Math.Abs(i - mid)) * o.StaggerMs;
            items.Add(new FanItem(i, center, rotation, delay, icon, label,
                edge == TaskbarEdge.Left ? LabelSide.Right : LabelSide.Left));
        }
        return items;
    }

    private static FanLayout Grid(int n, Point2 origin, TaskbarEdge edge, Rect2 wa, FanLayoutOptions o)
    {
        var inner = wa.Inflate(-o.Margin);
        var pad = o.GridPadding;
        var cellW = o.IconSize + o.GridCellExtraWidth;
        var cellH = o.IconSize + 12 + o.LabelHeight + 8;

        var cols = n <= 12 ? 4 : n <= 20 ? 5 : 6;
        cols = Math.Min(cols, n);
        while (cols > 1 && cols * cellW + 2 * pad > inner.Width)
            cols--;
        var rows = (n + cols - 1) / cols;

        var width = Math.Min(cols * cellW + 2 * pad, inner.Width);
        var contentH = rows * cellH;
        var vertical = edge is TaskbarEdge.Bottom or TaskbarEdge.Top;
        var available = vertical ? inner.Height - o.EdgeGap : inner.Height;
        var maxH = Math.Max(cellH + 2 * pad, Math.Min(available, wa.Height * o.GridMaxHeightRatio));
        maxH = Math.Min(maxH, inner.Height);
        var height = Math.Min(contentH + 2 * pad, maxH);
        var needsScroll = contentH + 2 * pad > height + 0.001;

        var popup = edge switch
        {
            TaskbarEdge.Bottom => new Rect2(origin.X - width / 2, origin.Y - o.EdgeGap - height, width, height),
            TaskbarEdge.Top => new Rect2(origin.X - width / 2, origin.Y + o.EdgeGap, width, height),
            TaskbarEdge.Left => new Rect2(origin.X + o.EdgeGap, origin.Y - height / 2, width, height),
            _ => new Rect2(origin.X - o.EdgeGap - width, origin.Y - height / 2, width, height),
        };
        popup = popup.Offset(
            ShiftInto(popup.Left, popup.Right, inner.Left, inner.Right),
            ShiftInto(popup.Top, popup.Bottom, inner.Top, inner.Bottom));

        var items = new List<FanItem>(n);
        for (var i = 0; i < n; i++)
        {
            var col = i % cols;
            var row = i / cols;
            var cx = popup.Left + pad + col * cellW + cellW / 2;
            var cy = popup.Top + pad + row * cellH + 6 + o.IconSize / 2;
            var center = new Point2(cx, cy);
            var icon = Rect2.FromCenter(center, o.IconSize, o.IconSize);
            var label = new Rect2(cx - cellW / 2 + 4, icon.Bottom + 6, cellW - 8, o.LabelHeight);
            var delay = Math.Min(row + col, 12) * o.StaggerMs / 2;
            items.Add(new FanItem(i, center, 0, delay, icon, label, LabelSide.Below));
        }

        var grid = new GridInfo(popup, cols, rows, cellW, cellH, contentH, needsScroll, pad);
        return new FanLayout(LayoutMode.Grid, edge, origin, items, popup.Inflate(o.ShadowPadding).Intersect(wa), grid);
    }

    /// <summary>Spostamento minimo per portare [lo, hi] dentro [min, max].</summary>
    private static double ShiftInto(double lo, double hi, double min, double max)
    {
        if (hi - lo > max - min)
            return min - lo;
        if (lo < min)
            return min - lo;
        if (hi > max)
            return max - hi;
        return 0;
    }
}
