namespace PixelCompanion.Core.Models;

public enum MovementSurfaceKind { DesktopFloor, WindowTop, CustomRegion }

public readonly record struct DesktopPoint(double X, double Y);

public readonly record struct DesktopRect(double X, double Y, double Width, double Height)
{
    public double Right => X + Width;
    public double Bottom => Y + Height;
    public bool IsValid => Width > 0 && Height > 0;
}

public sealed record MovementSurface(
    string Id,
    MovementSurfaceKind Kind,
    DesktopRect Bounds,
    long NativeHandle = 0,
    string ProcessName = "",
    int ZOrder = int.MaxValue,
    bool IsWalkable = true)
{
    public bool IsValidFor(double petWidth) => Bounds.IsValid && Bounds.Width >= petWidth;

    public double PetTop(double petHeight) =>
        Kind == MovementSurfaceKind.WindowTop ? Bounds.Y - petHeight : Bounds.Bottom - petHeight;
}

public readonly record struct SurfacePlacement(string SurfaceId, double X, double Y);

public readonly record struct WalkableRange(double MinimumX, double MaximumX)
{
    public bool IsValid => MaximumX >= MinimumX;
    public bool Contains(double x, double tolerance = 0.5) =>
        x >= MinimumX - tolerance && x <= MaximumX + tolerance;
}

public static class MovementGeometry
{
    public static bool TryPlace(
        MovementSurface surface,
        double requestedX,
        double petWidth,
        double petHeight,
        out SurfacePlacement placement)
    {
        placement = default;
        if (!surface.IsValidFor(petWidth) || petWidth <= 0 || petHeight <= 0)
        {
            return false;
        }

        var x = Math.Clamp(requestedX, surface.Bounds.X, surface.Bounds.Right - petWidth);
        placement = new SurfacePlacement(surface.Id, x, surface.PetTop(petHeight));
        return true;
    }

    public static MovementSurface? FindNearest(
        IEnumerable<MovementSurface> surfaces,
        DesktopPoint petBottomCenter,
        double petWidth)
    {
        return surfaces
            .Where(surface => surface.IsValidFor(petWidth))
            .OrderBy(surface => DistanceSquared(surface, petBottomCenter))
            .FirstOrDefault();
    }

    public static double RelativeX(MovementSurface surface, double petX, double petWidth)
    {
        var travel = surface.Bounds.Width - petWidth;
        return travel <= 0 ? 0 : Math.Clamp((petX - surface.Bounds.X) / travel, 0, 1);
    }

    public static double ResolveRelativeX(MovementSurface surface, double relativeX, double petWidth)
    {
        var travel = Math.Max(0, surface.Bounds.Width - petWidth);
        return surface.Bounds.X + (Math.Clamp(relativeX, 0, 1) * travel);
    }

    public static double HorizontalScale(bool movingLeft, bool frameFacesLeft) =>
        movingLeft == frameFacesLeft ? 1 : -1;

    public static IReadOnlyList<WalkableRange> GetWalkableRanges(
        MovementSurface surface,
        IEnumerable<MovementSurface> windowSurfaces,
        double petWidth)
    {
        if (!surface.IsValidFor(petWidth) || petWidth <= 0)
            return [];

        if (surface.Kind != MovementSurfaceKind.WindowTop)
            return [new WalkableRange(surface.Bounds.X, surface.Bounds.Right - petWidth)];

        var clearSegments = new List<(double Start, double End)>
        {
            (surface.Bounds.X, surface.Bounds.Right)
        };
        foreach (var obstacle in windowSurfaces.Where(candidate =>
                     candidate.Kind == MovementSurfaceKind.WindowTop &&
                     candidate.NativeHandle != surface.NativeHandle &&
                     candidate.ZOrder < surface.ZOrder &&
                     candidate.Bounds.Y <= surface.Bounds.Y &&
                     candidate.Bounds.Bottom > surface.Bounds.Y &&
                     candidate.Bounds.Right > surface.Bounds.X &&
                     candidate.Bounds.X < surface.Bounds.Right))
        {
            const double collisionMargin = 2;
            var blockedStart = Math.Max(surface.Bounds.X, obstacle.Bounds.X - collisionMargin);
            var blockedEnd = Math.Min(surface.Bounds.Right, obstacle.Bounds.Right + collisionMargin);
            var next = new List<(double Start, double End)>();
            foreach (var segment in clearSegments)
            {
                if (blockedEnd <= segment.Start || blockedStart >= segment.End)
                {
                    next.Add(segment);
                    continue;
                }

                if (blockedStart > segment.Start)
                    next.Add((segment.Start, Math.Min(blockedStart, segment.End)));
                if (blockedEnd < segment.End)
                    next.Add((Math.Max(blockedEnd, segment.Start), segment.End));
            }
            clearSegments = next;
            if (clearSegments.Count == 0) break;
        }

        return clearSegments
            .Where(segment => segment.End - segment.Start >= petWidth)
            .Select(segment => new WalkableRange(segment.Start, segment.End - petWidth))
            .ToArray();
    }

    public static WalkableRange? FindContainingRange(
        IEnumerable<WalkableRange> ranges,
        double x)
    {
        foreach (var range in ranges)
            if (range.IsValid && range.Contains(x))
                return range;
        return null;
    }

    public static WalkableRange? FindNearestRange(
        IEnumerable<WalkableRange> ranges,
        double x)
    {
        var ordered = ranges
            .Where(range => range.IsValid)
            .OrderBy(range => x < range.MinimumX
                ? range.MinimumX - x
                : x > range.MaximumX
                    ? x - range.MaximumX
                    : 0);
        foreach (var range in ordered)
            return range;
        return null;
    }

    private static double DistanceSquared(MovementSurface surface, DesktopPoint point)
    {
        var nearestX = Math.Clamp(point.X, surface.Bounds.X, surface.Bounds.Right);
        var groundY = surface.Kind == MovementSurfaceKind.WindowTop
            ? surface.Bounds.Y
            : surface.Bounds.Bottom;
        var dx = point.X - nearestX;
        var dy = point.Y - groundY;
        return (dx * dx) + (dy * dy);
    }
}
