using System.Windows;
using System.Windows.Media;

namespace PixelMe.Native;

internal static class RegionBuilder
{
    public static IntPtr FromGeometry(Geometry geometry, double scaleX, double scaleY)
    {
        var flat = geometry.GetFlattenedPathGeometry(0.25, ToleranceType.Absolute);
        var points = new List<NativeMethods.POINT>();

        foreach (var figure in flat.Figures)
        {
            Add(figure.StartPoint);
            foreach (var segment in figure.Segments)
            {
                switch (segment)
                {
                    case PolyLineSegment poly:
                        foreach (var point in poly.Points)
                        {
                            Add(point);
                        }

                        break;
                    case LineSegment line:
                        Add(line.Point);
                        break;
                }
            }
        }

        return points.Count < 3
            ? IntPtr.Zero
            : NativeMethods.CreatePolygonRgn(points.ToArray(), points.Count, NativeMethods.WINDING);

        void Add(Point point) => points.Add(new NativeMethods.POINT
        {
            X = (int)Math.Round(point.X * scaleX),
            Y = (int)Math.Round(point.Y * scaleY),
        });
    }
}
