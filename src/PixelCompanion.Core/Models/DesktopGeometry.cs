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
    string ProcessName = "")
{
    public bool IsValidFor(double petWidth) => Bounds.IsValid && Bounds.Width >= petWidth;

    public double PetTop(double petHeight) =>
        Kind == MovementSurfaceKind.WindowTop ? Bounds.Y - petHeight : Bounds.Bottom - petHeight;
}

public readonly record struct SurfacePlacement(string SurfaceId, double X, double Y);

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
