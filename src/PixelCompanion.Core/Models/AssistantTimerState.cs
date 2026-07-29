namespace PixelCompanion.Core.Models;

public enum AssistantTimerKind { General, Focus, Rest }

public sealed record AssistantTimerState
{
    public int SchemaVersion { get; init; } = 1;
    public bool IsRunning { get; init; }
    public AssistantTimerKind Kind { get; init; } = AssistantTimerKind.General;
    public DateTimeOffset? EndsAtUtc { get; init; }
    public int DurationMinutes { get; init; }

    public TimeSpan Remaining(DateTimeOffset now) =>
        IsRunning && EndsAtUtc is { } end && end > now ? end - now : TimeSpan.Zero;
}
