using System.Runtime.InteropServices;
using FanStack.App.Interop;
using FanStack.Core.Geometry;

namespace FanStack.App.Services;

/// <summary>
/// Monitor sotto il cursore, la sua scala DPI e la posizione della taskbar.
/// Le geometrie sono convertite in DIP (pixel fisici / scala) per il calcolo del layout.
/// </summary>
internal sealed record ScreenContext(TaskbarPlacement Placement, double Scale, Rect2 MonitorDip, bool IsPrimary)
{
    /// <summary>Converte un rettangolo in DIP nei pixel fisici dello schermo.</summary>
    public NativeMethods.RECT ToPhysical(Rect2 dip) => new()
    {
        Left = (int)Math.Round(dip.Left * Scale),
        Top = (int)Math.Round(dip.Top * Scale),
        Right = (int)Math.Round(dip.Right * Scale),
        Bottom = (int)Math.Round(dip.Bottom * Scale),
    };
}

internal static class ScreenInfo
{
    public static ScreenContext Capture()
    {
        if (!NativeMethods.GetCursorPos(out var cursor))
            cursor = new NativeMethods.POINT();

        var hmon = NativeMethods.MonitorFromPoint(cursor, NativeMethods.MONITOR_DEFAULTTONEAREST);
        var mi = new NativeMethods.MONITORINFO { cbSize = Marshal.SizeOf<NativeMethods.MONITORINFO>() };
        if (!NativeMethods.GetMonitorInfo(hmon, ref mi))
            throw new InvalidOperationException("Impossibile leggere le informazioni del monitor.");

        var scale = 1.0;
        if (NativeMethods.GetDpiForMonitor(hmon, NativeMethods.MDT_EFFECTIVE_DPI, out var dpiX, out _) == 0 && dpiX > 0)
            scale = dpiX / 96.0;

        var isPrimary = (mi.dwFlags & NativeMethods.MONITORINFOF_PRIMARY) != 0;
        var hint = isPrimary ? PrimaryTaskbarEdge() : null;

        var monitor = ToDip(mi.rcMonitor, scale);
        var work = ToDip(mi.rcWork, scale);

        // Solo per le prove: Windows 11 non permette di spostare la taskbar, quindi si può simulare
        // una taskbar da 48 DIP su un altro bordo con FANSTACK_SIMULATE_EDGE=Left|Top|Right|Bottom.
        if (Enum.TryParse<TaskbarEdge>(Environment.GetEnvironmentVariable("FANSTACK_SIMULATE_EDGE"), true, out var simulated))
        {
            const double bar = 48;
            work = simulated switch
            {
                TaskbarEdge.Bottom => new Rect2(monitor.X, monitor.Y, monitor.Width, monitor.Height - bar),
                TaskbarEdge.Top => new Rect2(monitor.X, monitor.Y + bar, monitor.Width, monitor.Height - bar),
                TaskbarEdge.Left => new Rect2(monitor.X + bar, monitor.Y, monitor.Width - bar, monitor.Height),
                _ => new Rect2(monitor.X, monitor.Y, monitor.Width - bar, monitor.Height),
            };
            hint = simulated;
            Log.Info($"Taskbar simulata sul bordo {simulated}.");
        }
        var cursorDip = new Point2(cursor.X / scale, cursor.Y / scale);
        var placement = TaskbarLocator.Resolve(monitor, work, cursorDip, hint);

        Log.Info($"Schermo: monitor={mi.rcMonitor.Left},{mi.rcMonitor.Top},{mi.rcMonitor.Right},{mi.rcMonitor.Bottom} " +
                 $"lavoro={mi.rcWork.Left},{mi.rcWork.Top},{mi.rcWork.Right},{mi.rcWork.Bottom} scala={scale:0.##} " +
                 $"cursore={cursor.X},{cursor.Y} bordo={placement.Edge} autohide={placement.AutoHide}");
        return new ScreenContext(placement, scale, monitor, isPrimary);
    }

    /// <summary>Bordo della taskbar principale secondo ABM_GETTASKBARPOS.</summary>
    private static TaskbarEdge? PrimaryTaskbarEdge()
    {
        var abd = new NativeMethods.APPBARDATA { cbSize = Marshal.SizeOf<NativeMethods.APPBARDATA>() };
        if (NativeMethods.SHAppBarMessage(NativeMethods.ABM_GETTASKBARPOS, ref abd) == UIntPtr.Zero)
            return null;
        return abd.uEdge switch
        {
            NativeMethods.ABE_LEFT => TaskbarEdge.Left,
            NativeMethods.ABE_TOP => TaskbarEdge.Top,
            NativeMethods.ABE_RIGHT => TaskbarEdge.Right,
            NativeMethods.ABE_BOTTOM => TaskbarEdge.Bottom,
            _ => null,
        };
    }

    private static Rect2 ToDip(NativeMethods.RECT r, double scale) =>
        Rect2.FromLTRB(r.Left / scale, r.Top / scale, r.Right / scale, r.Bottom / scale);
}
