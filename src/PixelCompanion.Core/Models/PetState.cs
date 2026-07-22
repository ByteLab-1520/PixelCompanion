namespace PixelCompanion.Core.Models;

public enum ActivityState { Normal, Idle, Walking, Sitting, Sleeping, Playing, ListeningToMusic, WatchingVideo, Charging, BeingDragged }
public enum MoodState { Neutral, Happy, Sad, Tired, Hungry }

public sealed record PetState
{
    public int SchemaVersion { get; init; } = 1;
    public double Hunger { get; init; } = 15;
    public double Cleanliness { get; init; } = 85;
    public double Happiness { get; init; } = 75;
    public double Fatigue { get; init; } = 10;
    public double Affection { get; init; } = 50;
    public ActivityState Activity { get; init; } = ActivityState.Idle;
    public MoodState Mood { get; init; } = MoodState.Neutral;
    public DateTimeOffset LastUpdatedUtc { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastInteractionUtc { get; init; }
}

public sealed record EnvironmentSnapshot
{
    public DateTimeOffset Now { get; init; } = DateTimeOffset.Now;
    public bool IsDragging { get; init; }
    public bool IsHidden { get; init; }
    public bool IsOutsideRegion { get; init; }
    public bool DoNotDisturb { get; init; }
    public bool DirectCommandPending { get; init; }
    public bool IsMediaPlaying { get; init; }
    public bool IsVideo { get; init; }
    public bool IsCharging { get; init; }
    public bool HasBattery { get; init; }
    public double? BatteryPercent { get; init; }
    public TimeSpan IdleTime { get; init; }
    public bool IsFullScreen { get; init; }
    public bool IsHighLoad { get; init; }
}

