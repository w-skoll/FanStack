namespace FanStack.Core.Geometry;

/// <summary>Bordo dello schermo su cui si trova la taskbar.</summary>
public enum TaskbarEdge
{
    Bottom,
    Top,
    Left,
    Right,
}

/// <summary>Punto 2D (unità indipendenti: DIP o pixel, a scelta del chiamante).</summary>
public readonly record struct Point2(double X, double Y)
{
    public Point2 Offset(double dx, double dy) => new(X + dx, Y + dy);
}

/// <summary>Rettangolo 2D allineato agli assi.</summary>
public readonly record struct Rect2(double X, double Y, double Width, double Height)
{
    public double Left => X;
    public double Top => Y;
    public double Right => X + Width;
    public double Bottom => Y + Height;
    public Point2 Center => new(X + Width / 2, Y + Height / 2);
    public bool IsEmpty => Width <= 0 || Height <= 0;

    public static Rect2 FromLTRB(double left, double top, double right, double bottom) =>
        new(left, top, right - left, bottom - top);

    public static Rect2 FromCenter(Point2 center, double width, double height) =>
        new(center.X - width / 2, center.Y - height / 2, width, height);

    public Rect2 Offset(double dx, double dy) => this with { X = X + dx, Y = Y + dy };

    public Rect2 Inflate(double amount) =>
        new(X - amount, Y - amount, Math.Max(0, Width + 2 * amount), Math.Max(0, Height + 2 * amount));

    public Rect2 Union(Rect2 other) =>
        FromLTRB(Math.Min(Left, other.Left), Math.Min(Top, other.Top), Math.Max(Right, other.Right), Math.Max(Bottom, other.Bottom));

    public Rect2 Intersect(Rect2 other)
    {
        var l = Math.Max(Left, other.Left);
        var t = Math.Max(Top, other.Top);
        var r = Math.Min(Right, other.Right);
        var b = Math.Min(Bottom, other.Bottom);
        return r <= l || b <= t ? new Rect2(l, t, 0, 0) : FromLTRB(l, t, r, b);
    }

    /// <summary>Vero se <paramref name="inner"/> sta tutto dentro (con tolleranza numerica).</summary>
    public bool Contains(Rect2 inner, double tolerance = 0.001) =>
        inner.Left >= Left - tolerance && inner.Top >= Top - tolerance &&
        inner.Right <= Right + tolerance && inner.Bottom <= Bottom + tolerance;

    public bool Contains(Point2 p) => p.X >= Left && p.X <= Right && p.Y >= Top && p.Y <= Bottom;

    public bool Intersects(Rect2 other, double tolerance = 0.001) =>
        Left < other.Right - tolerance && other.Left < Right - tolerance &&
        Top < other.Bottom - tolerance && other.Top < Bottom - tolerance;
}
