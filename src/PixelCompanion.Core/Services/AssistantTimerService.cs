using PixelCompanion.Core.Models;

namespace PixelCompanion.Core.Services;

public sealed class AssistantTimerService
{
    public AssistantTimerState Start(AssistantTimerKind kind, int minutes, DateTimeOffset now)
    {
        var safeMinutes = Math.Clamp(minutes, 1, 24 * 60);
        return new AssistantTimerState
        {
            IsRunning = true,
            Kind = kind,
            DurationMinutes = safeMinutes,
            EndsAtUtc = now.AddMinutes(safeMinutes)
        };
    }

    public AssistantTimerState Cancel() => new();

    public bool IsComplete(AssistantTimerState state, DateTimeOffset now) =>
        state.IsRunning && state.EndsAtUtc is { } end && end <= now;
}
