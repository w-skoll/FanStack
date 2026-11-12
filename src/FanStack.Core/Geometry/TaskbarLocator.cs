namespace FanStack.Core.Geometry;

/// <summary>Dove si trova la taskbar e da dove deve partire il ventaglio.</summary>
/// <param name="Edge">Bordo della taskbar.</param>
/// <param name="WorkArea">Area utilizzabile (se la taskbar si nasconde da sola viene riservato lo spazio).</param>
/// <param name="Origin">Punto di origine del ventaglio, agganciato al bordo della taskbar.</param>
/// <param name="AutoHide">Vero se la taskbar non occupa spazio (nascosta automaticamente).</param>
public sealed record TaskbarPlacement(TaskbarEdge Edge, Rect2 WorkArea, Point2 Origin, bool AutoHide);

/// <summary>Rileva il bordo della taskbar su un monitor e aggancia il punto di origine.</summary>
public static class TaskbarLocator
{
    /// <summary>Spazio riservato quando la taskbar è nascosta automaticamente.</summary>
    public const double AutoHideReserve = 48;

    /// <param name="monitor">Rettangolo dell'intero monitor.</param>
    /// <param name="workArea">Area di lavoro del monitor (monitor meno la taskbar).</param>
    /// <param name="cursor">Posizione del cursore al momento del clic.</param>
    /// <param name="hintEdge">Bordo suggerito (per esempio da ABM_GETTASKBARPOS), usato se il monitor non ha rientri.</param>
    public static TaskbarPlacement Resolve(Rect2 monitor, Rect2 workArea, Point2 cursor, TaskbarEdge? hintEdge = null)
    {
        if (workArea.IsEmpty)
            workArea = monitor;

        var insets = new (TaskbarEdge Edge, double Size)[]
        {
            (TaskbarEdge.Bottom, monitor.Bottom - workArea.Bottom),
            (TaskbarEdge.Top, workArea.Top - monitor.Top),
            (TaskbarEdge.Left, workArea.Left - monitor.Left),
            (TaskbarEdge.Right, monitor.Right - workArea.Right),
        };
        var best = insets.MaxBy(i => i.Size);

        TaskbarEdge edge;
        var autoHide = false;
        var area = workArea;
        if (best.Size > 0.5)
        {
            edge = best.Edge;
        }
        else
        {
            // Nessun rientro: taskbar nascosta automaticamente. Usa il suggerimento o il bordo più vicino al cursore.
            autoHide = true;
            edge = hintEdge ?? NearestEdge(monitor, cursor);
            area = Reserve(workArea, edge, Math.Min(AutoHideReserve, ExtentAlong(workArea, edge) / 4));
        }

        return new TaskbarPlacement(edge, area, SnapOrigin(cursor, edge, area), autoHide);
    }

    /// <summary>Aggancia il cursore al bordo dell'area di lavoro dal lato della taskbar.</summary>
    public static Point2 SnapOrigin(Point2 cursor, TaskbarEdge edge, Rect2 workArea)
    {
        var x = Math.Clamp(cursor.X, workArea.Left, workArea.Right);
        var y = Math.Clamp(cursor.Y, workArea.Top, workArea.Bottom);
        return edge switch
        {
            TaskbarEdge.Bottom => new Point2(x, workArea.Bottom),
            TaskbarEdge.Top => new Point2(x, workArea.Top),
            TaskbarEdge.Left => new Point2(workArea.Left, y),
            _ => new Point2(workArea.Right, y),
        };
    }

    public static TaskbarEdge NearestEdge(Rect2 monitor, Point2 p)
    {
        var d = new (TaskbarEdge Edge, double Dist)[]
        {
            (TaskbarEdge.Bottom, Math.Abs(monitor.Bottom - p.Y)),
            (TaskbarEdge.Top, Math.Abs(p.Y - monitor.Top)),
            (TaskbarEdge.Left, Math.Abs(p.X - monitor.Left)),
            (TaskbarEdge.Right, Math.Abs(monitor.Right - p.X)),
        };
        return d.MinBy(x => x.Dist).Edge;
    }

    private static double ExtentAlong(Rect2 r, TaskbarEdge edge) =>
        edge is TaskbarEdge.Bottom or TaskbarEdge.Top ? r.Height : r.Width;

    private static Rect2 Reserve(Rect2 r, TaskbarEdge edge, double amount) => edge switch
    {
        TaskbarEdge.Bottom => r with { Height = r.Height - amount },
        TaskbarEdge.Top => new Rect2(r.X, r.Y + amount, r.Width, r.Height - amount),
        TaskbarEdge.Left => new Rect2(r.X + amount, r.Y, r.Width - amount, r.Height),
        _ => r with { Width = r.Width - amount },
    };
}
