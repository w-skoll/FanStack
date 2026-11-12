namespace FanStack.Core.Geometry;

/// <summary>Direzione di una freccia della tastiera.</summary>
public enum NavDirection
{
    Up,
    Down,
    Left,
    Right,
}

/// <summary>
/// Navigazione con le frecce basata sulla posizione delle icone: funziona allo stesso modo
/// per ventaglio e griglia, qualunque sia il bordo della taskbar.
/// </summary>
public static class KeyboardNavigator
{
    /// <summary>Indice della voce iniziale: la più vicina all'origine (la taskbar).</summary>
    public static int Initial(IReadOnlyList<Point2> centers, Point2 origin)
    {
        if (centers.Count == 0)
            return -1;
        var best = 0;
        for (var i = 1; i < centers.Count; i++)
            if (Dist2(centers[i], origin) < Dist2(centers[best], origin))
                best = i;
        return best;
    }

    /// <summary>Prossima voce nella direzione indicata, o la stessa se non ce n'è.</summary>
    public static int Next(IReadOnlyList<Point2> centers, int current, NavDirection direction)
    {
        if (centers.Count == 0)
            return -1;
        if (current < 0 || current >= centers.Count)
            return 0;

        var from = centers[current];
        var best = current;
        var bestScore = double.MaxValue;
        for (var i = 0; i < centers.Count; i++)
        {
            if (i == current)
                continue;
            var dx = centers[i].X - from.X;
            var dy = centers[i].Y - from.Y;
            // Distanza lungo la direzione richiesta e scostamento laterale.
            var (primary, lateral) = direction switch
            {
                NavDirection.Up => (-dy, Math.Abs(dx)),
                NavDirection.Down => (dy, Math.Abs(dx)),
                NavDirection.Left => (-dx, Math.Abs(dy)),
                _ => (dx, Math.Abs(dy)),
            };
            // Solo voci davvero in quella direzione (cono di 60° circa).
            if (primary <= 0.5 || lateral > primary * 1.8)
                continue;
            var score = primary + lateral * 2;
            if (score < bestScore)
            {
                bestScore = score;
                best = i;
            }
        }
        return best;
    }

    private static double Dist2(Point2 a, Point2 b) => (a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y);
}
