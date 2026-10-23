using System.Windows;
using System.Windows.Media;
using System.Collections.Concurrent;

namespace Dial.Models;

public static class DialGeometry
{
    private static readonly ConcurrentDictionary<(int Index, int Count), Geometry> SectorCache = new();
    private static readonly ConcurrentDictionary<(int Count, bool ShowCenter), Geometry> SurfaceCache = new();
    private static readonly ConcurrentDictionary<(int Index, int Count, bool IconOutside), (Rect Icon, Rect Label)?> StackedContentCache = new();
    private static readonly ConcurrentDictionary<(int Index, int Count, double Radius, double Width, double Height), Rect> ContentCache = new();
    public const double OuterRadius = 143;
    public const double InnerRadius = 76;
    public const double CenterRadius = 26;
    public const double SectorGap = 5;
    public const double OutlineThickness = 1.75;
    public const double MaximumCenterRadius = InnerRadius - SectorGap - OutlineThickness;
    public const double CanvasPadding = 16;
    public const double Size = (OuterRadius + CanvasPadding) * 2;
    public const double Center = Size / 2;
    public const double RingWidth = OuterRadius - InnerRadius;
    public const double InnerContentRadius = 92;
    public const double OuterContentRadius = 126;
    public const double ContentHeight = 36;
    public const double IconSize = 24;
    public const double PreferredIconWidth = 40;
    public const double PreferredLabelWidth = 96;
    public const double ContentClearance = 4;
    public const double MinimumLabelFontSize = 9.5;
    public const double MaximumLabelFontSize = 11;
    public const double SelectedSectorScale = 1.03;
    public const double SelectedOutlineThickness = 1.95;
    public const double IconSeparatorWidth = SectorGap + 2 * OutlineThickness;
    public const int MaximumItems = 16;
    public const int MaximumTextLength = 64;

    public static double CenterAngle(int index, int count) => -90 + index * 360.0 / count;

    public static double BoundaryAngle(int index, int count) => CenterAngle(index, count) - 180.0 / count;

    public static int HitTest(Point position, int count)
    {
        if (count is < 1 or > MaximumItems) return -1;
        double x = position.X - Center;
        double y = position.Y - Center;
        if (!double.IsFinite(x) || !double.IsFinite(y)) return -1;
        double radiusSquared = x * x + y * y;
        if (radiusSquared < InnerRadius * InnerRadius || radiusSquared > OuterRadius * OuterRadius)
            return -1;

        double sweep = 360.0 / count;
        double angle = Math.Atan2(y, x) * 180 / Math.PI;
        double normalized = (angle + 90 + sweep / 2 + 360) % 360;
        int index = (int)(normalized / sweep) % count;
        return CreateSector(index, count).FillContains(position) ? index : -1;
    }

    public static Point Polar(double radius, double degrees)
    {
        double radians = degrees * Math.PI / 180;
        return new Point(Center + radius * Math.Cos(radians), Center + radius * Math.Sin(radians));
    }

    public static Rect GetContentBounds(int index, int count, double radius, double preferredWidth, double height = ContentHeight)
        => ContentCache.GetOrAdd((index, count, radius, preferredWidth, height),
            key => BuildContentBounds(key.Index, key.Count, key.Radius, key.Width, key.Height));

    private static Rect BuildContentBounds(int index, int count, double radius, double preferredWidth, double height)
    {
        var sector = CreateSector(index, count).GetOutlinedPathGeometry();
        double angle = CenterAngle(index, count) * Math.PI / 180;
        double rowDistance = OuterContentRadius - InnerContentRadius;
        double verticalDistance = Math.Abs(rowDistance * Math.Sin(angle));
        if (radius == InnerContentRadius || radius == OuterContentRadius)
        {
            if (verticalDistance >= (IconSize + 12 + ContentClearance) / 2)
                height = Math.Min(height, preferredWidth <= PreferredIconWidth ? IconSize
                    : Math.Max(12, Math.Floor((2 * verticalDistance - IconSize - ContentClearance) / 4) * 4));
            else
            {
                double horizontalDistance = Math.Abs(rowDistance * Math.Cos(angle));
                preferredWidth = Math.Min(preferredWidth, preferredWidth <= PreferredIconWidth
                    ? IconSize : 2 * horizontalDistance - IconSize - ContentClearance);
            }
        }
        Point center = Polar(radius, CenterAngle(index, count));
        for (double rowHeight = height; rowHeight >= 12; rowHeight -= 4)
            for (double width = Math.Floor(preferredWidth / 4) * 4; width >= 12; width -= 4)
            {
                var bounds = new Rect(center.X - width / 2, center.Y - rowHeight / 2, width, rowHeight);
                if (!FitsContentBounds(bounds, index, count)) continue;
                Rect padded = bounds;
                padded.Inflate(ContentClearance, ContentClearance);
                var rectangle = new RectangleGeometry(padded);
                if (sector.FillContainsWithDetail(rectangle) == IntersectionDetail.FullyContains)
                    return bounds;
            }
        throw new InvalidOperationException($"The content row does not fit inside sector {index}/{count} at radius {radius} (width {preferredWidth}, height {height}).");
    }

    public static (Rect Icon, Rect Label)? GetStackedContentBounds(int index, int count, bool iconOutside)
        => StackedContentCache.GetOrAdd((index, count, iconOutside),
            key => BuildStackedContentBounds(key.Index, key.Count, key.IconOutside));

    private static (Rect Icon, Rect Label)? BuildStackedContentBounds(int index, int count, bool iconOutside)
    {
        double angle = CenterAngle(index, count) * Math.PI / 180;
        if (Math.Abs(Math.Cos(angle)) < 0.35) return null;
        bool horizontal = Math.Abs(Math.Sin(angle)) < 0.35;
        double radialDirection = iconOutside ? 1 : -1;
        double direction = (horizontal ? -1 : Math.Sign(Math.Sin(angle))) * radialDirection;
        var sector = CreateSector(index, count).GetOutlinedPathGeometry();
        bool Fits(Rect bounds)
        {
            if (!FitsContentBounds(bounds, index, count)) return false;
            bounds.Inflate(ContentClearance, ContentClearance);
            var rectangle = new RectangleGeometry(bounds);
            return sector.FillContainsWithDetail(rectangle) == IntersectionDetail.FullyContains;
        }
        for (double width = 64; width >= 24; width -= 2)
            foreach (double iconSize in new[] { 20.0, 18 })
            foreach (double labelHeight in new[] { 48.0, 36, 24 })
                for (double radius = InnerRadius + 20; radius <= OuterRadius - 20; radius += 2)
                foreach (double offset in horizontal ? new[] { 2.0 } : new[] { -8.0, -6, -4, -2, 0, 2, 4, 6, 8, 10, 12 })
                {
                    Point center = Polar(radius, CenterAngle(index, count));
                    double xOffset = Math.Cos(angle) * offset * radialDirection;
                    var icon = new Rect(center.X + xOffset - iconSize / 2,
                        center.Y + direction * (labelHeight / 2 + 2) - iconSize / 2, iconSize, iconSize);
                    var label = new Rect(center.X - xOffset - width / 2,
                        center.Y - direction * (iconSize / 2 + 2) - labelHeight / 2, width, labelHeight);
                    if (icon.IntersectsWith(label) || !Fits(icon) || !Fits(label)) continue;
                    return (icon, label);
                }
        return null;
    }

    private static bool FitsContentBounds(Rect bounds, int index, int count)
    {
        bounds.Inflate(ContentClearance, ContentClearance);
        double nearX = Math.Max(Math.Abs(bounds.X + bounds.Width / 2 - Center) - bounds.Width / 2, 0);
        double nearY = Math.Max(Math.Abs(bounds.Y + bounds.Height / 2 - Center) - bounds.Height / 2, 0);
        if (nearX * nearX + nearY * nearY < InnerRadius * InnerRadius) return false;
        double start = BoundaryAngle(index, count) * Math.PI / 180;
        double end = BoundaryAngle(index + 1, count) * Math.PI / 180;
        foreach (Point corner in new[] { bounds.TopLeft, bounds.TopRight, bounds.BottomLeft, bounds.BottomRight })
        {
            double x = corner.X - Center, y = corner.Y - Center;
            if (x * x + y * y > OuterRadius * OuterRadius) return false;
            double clearance = (SectorGap + OutlineThickness) / 2;
            if (count > 1 && (Math.Cos(start) * y - Math.Sin(start) * x < clearance ||
                Math.Sin(end) * x - Math.Cos(end) * y < clearance)) return false;
        }
        return true;
    }

    public static Geometry CreateSector(int index, int count)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(count, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(count, MaximumItems);
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, count);
        return SectorCache.GetOrAdd((index, count), key => BuildSector(key.Index, key.Count, SectorGap + OutlineThickness));
    }

    public static Geometry CreateIconSurface(double gap)
    {
        if (!double.IsFinite(gap)) throw new ArgumentOutOfRangeException(nameof(gap));
        ArgumentOutOfRangeException.ThrowIfNegative(gap);
        Geometry surface = Geometry.Empty;
        for (int index = 0; index < 8; index++)
            surface = new CombinedGeometry(GeometryCombineMode.Union, surface, BuildSector(index, 8, gap));
        surface.Freeze();
        return surface;
    }

    public static Point SectorScaleCenter(int index, int count)
    {
        Rect bounds = CreateSector(index, count).Bounds;
        return new Point(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2);
    }

    public static double GetSelectedSectorScale(int index, int count)
    {
        _ = CreateSector(index, count);
        return SelectedSectorScale;
    }

    public static double GetSectorOutline(int index, int count, double scale) =>
        OutlineThickness + (SelectedOutlineThickness - OutlineThickness) *
        Math.Clamp((scale - 1) / (GetSelectedSectorScale(index, count) - 1), 0, 1);

    public static Geometry CreateSectorSurface(int index, int count, double outline = OutlineThickness)
    {
        Geometry sector = CreateSector(index, count);
        var surface = new CombinedGeometry(GeometryCombineMode.Union, sector,
            sector.GetWidenedPathGeometry(new Pen(Brushes.Black, outline)));
        surface.Freeze();
        return surface;
    }

    private static Geometry BuildSector(int index, int count, double cutWidth)
    {
        if (count == 1)
        {
            var ring = new CombinedGeometry(GeometryCombineMode.Exclude,
                new EllipseGeometry(new Point(Center, Center), OuterRadius, OuterRadius),
                new EllipseGeometry(new Point(Center, Center), InnerRadius, InnerRadius));
            ring.Freeze();
            return ring;
        }

        double start = BoundaryAngle(index, count);
        double end = BoundaryAngle(index + 1, count);
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(Polar(OuterRadius, start), true, true);
            context.ArcTo(Polar(OuterRadius, end), new Size(OuterRadius, OuterRadius), 0,
                false, SweepDirection.Clockwise, true, false);
            context.LineTo(Polar(InnerRadius, end), true, false);
            context.ArcTo(Polar(InnerRadius, start), new Size(InnerRadius, InnerRadius), 0,
                false, SweepDirection.Counterclockwise, true, false);
        }
        geometry.Freeze();
        var cuts = new GeometryGroup();
        for (int boundary = 0; boundary < count; boundary++)
        {
            var line = new LineGeometry(new Point(Center, Center),
                Polar(OuterRadius + OutlineThickness, BoundaryAngle(boundary, count)));
            cuts.Children.Add(line.GetWidenedPathGeometry(new Pen(Brushes.Black, cutWidth)));
        }
        var separated = new CombinedGeometry(GeometryCombineMode.Exclude, geometry, cuts);
        separated.Freeze();
        return separated;
    }

    public static Geometry CreateSurface(int count, bool showCenter = true)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(count, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(count, MaximumItems);
        return SurfaceCache.GetOrAdd((count, showCenter), key => BuildSurface(key.Count, key.ShowCenter));
    }

    private static Geometry BuildSurface(int count, bool showCenter)
    {
        double centerEdge = CenterRadius + OutlineThickness / 2;
        Geometry surface = showCenter
            ? new EllipseGeometry(new Point(Center, Center), centerEdge, centerEdge)
            : Geometry.Empty;
        for (int index = 0; index < count; index++)
        {
            surface = new CombinedGeometry(GeometryCombineMode.Union, surface, CreateSectorSurface(index, count));
        }
        surface.Freeze();
        return surface;
    }
}
