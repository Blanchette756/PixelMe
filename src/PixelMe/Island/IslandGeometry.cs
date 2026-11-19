using System.Windows;
using System.Windows.Media;

namespace PixelMe.Island;

public static class IslandGeometry
{
    public static Geometry Outline(double width, double height, double radius, double shoulder, bool notch, double inset)
    {
        return notch
            ? Notch(width, height, radius, shoulder, inset)
            : Pill(width, height, radius, inset);
    }

    private static Geometry Pill(double width, double height, double radius, double inset)
    {
        var w = Math.Max(1, width - (inset * 2));
        var h = Math.Max(1, height - (inset * 2));
        var r = Math.Clamp(radius - inset, 0, Math.Min(w, h) / 2);
        var geometry = new RectangleGeometry(new Rect(inset, inset, w, h), r, r);
        geometry.Freeze();
        return geometry;
    }

    private static Geometry Notch(double width, double height, double radius, double shoulder, double inset)
    {
        var left = shoulder + inset;
        var right = shoulder + width - inset;
        var bottom = Math.Max(1, height - inset);
        var r = Math.Clamp(radius - inset, 0, Math.Min(right - left, bottom) / 2);
        var s = shoulder;

        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            if (s > 0)
            {
                var ear = new Size(s, s);
                context.BeginFigure(new Point(left - s, 0), isFilled: true, isClosed: true);
                context.LineTo(new Point(right + s, 0), isStroked: false, isSmoothJoin: false);
                context.ArcTo(new Point(right, s), ear, 0, false, SweepDirection.Counterclockwise, true, false);
            }
            else
            {
                context.BeginFigure(new Point(left, 0), isFilled: true, isClosed: true);
                context.LineTo(new Point(right, 0), isStroked: false, isSmoothJoin: false);
            }

            var corner = new Size(r, r);
            context.LineTo(new Point(right, bottom - r), true, false);
            context.ArcTo(new Point(right - r, bottom), corner, 0, false, SweepDirection.Clockwise, true, false);
            context.LineTo(new Point(left + r, bottom), true, false);
            context.ArcTo(new Point(left, bottom - r), corner, 0, false, SweepDirection.Clockwise, true, false);

            if (s > 0)
            {
                context.LineTo(new Point(left, s), true, false);
                context.ArcTo(new Point(left - s, 0), new Size(s, s), 0, false, SweepDirection.Counterclockwise, true, false);
            }
            else
            {
                context.LineTo(new Point(left, 0), true, false);
            }
        }

        geometry.Freeze();
        return geometry;
    }
}
