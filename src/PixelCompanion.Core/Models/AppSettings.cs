namespace PixelCompanion.Core.Models;

public enum MovementSpeed { VerySlow, Slow, Normal, Fast, Custom }
public enum RegionMode { CurrentMonitor, SelectedMonitor, AllMonitors, Custom }
public enum DisconnectedRegionPolicy { StayInCurrent, NearestRegion, TimedTransition, ExitAndReenter }

public sealed record AppSettings
{
    public int SchemaVersion { get; init; } = 1;
    public string Language { get; init; } = "auto";
    public string ActiveCharacterId { get; init; } = "default-cat";
    public bool CharacterVisible { get; init; } = true;
    public bool BehaviorPaused { get; init; }
    public bool AlwaysOnTop { get; init; } = true;
    public bool ClickThrough { get; init; }
    public bool SoundEnabled { get; init; } = true;
    public bool DoNotDisturb { get; init; }
    public bool IncludeSystemAreas { get; init; }
    public bool AutoStart { get; init; }
    public double CharacterScale { get; init; } = 2;
    public double Opacity { get; init; } = 1;
    public double DialogueFrequency { get; init; } = 0.5;
    public MovementSpeed MovementSpeed { get; init; } = MovementSpeed.Slow;
    public double CustomPixelsPerSecond { get; init; } = 35;
    public RegionMode RegionMode { get; init; } = RegionMode.CurrentMonitor;
    public DisconnectedRegionPolicy DisconnectedRegionPolicy { get; init; } = DisconnectedRegionPolicy.StayInCurrent;
}

public sealed record MovementRegion(string Id, string Name, double X, double Y, double Width, double Height)
{
    public bool IsValid => Width > 0 && Height > 0;
}

