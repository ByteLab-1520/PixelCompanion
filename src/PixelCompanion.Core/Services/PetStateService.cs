using PixelCompanion.Core.Models;

namespace PixelCompanion.Core.Services;

public sealed class PetStateService
{
    public static readonly TimeSpan MaximumOfflineProgress = TimeSpan.FromHours(12);

    public PetState ApplyElapsed(PetState state, DateTimeOffset now)
    {
        var elapsed = now - state.LastUpdatedUtc;
        if (elapsed < TimeSpan.Zero) elapsed = TimeSpan.Zero;
        if (elapsed > MaximumOfflineProgress) elapsed = MaximumOfflineProgress;
        var hours = elapsed.TotalHours;

        return state with
        {
            Hunger = Clamp(state.Hunger + 2.2 * hours),
            Cleanliness = Clamp(state.Cleanliness - 0.9 * hours),
            Happiness = Clamp(state.Happiness - 0.45 * hours),
            Fatigue = Clamp(state.Fatigue + 1.4 * hours),
            Affection = Clamp(state.Affection - 0.08 * hours),
            LastUpdatedUtc = now
        };
    }

    public PetState Feed(PetState state, DateTimeOffset now) => Interact(state with
    {
        Hunger = Clamp(state.Hunger - 28), Happiness = Clamp(state.Happiness + 4)
    }, now);

    public PetState Pet(PetState state, DateTimeOffset now) => Interact(state with
    {
        Happiness = Clamp(state.Happiness + 8), Affection = Clamp(state.Affection + 2)
    }, now);

    public PetState Play(PetState state, DateTimeOffset now) => Interact(state with
    {
        Happiness = Clamp(state.Happiness + 14), Fatigue = Clamp(state.Fatigue + 8), Hunger = Clamp(state.Hunger + 4)
    }, now);

    public PetState Clean(PetState state, DateTimeOffset now) => Interact(state with
    {
        Cleanliness = Clamp(state.Cleanliness + 35), Happiness = Clamp(state.Happiness + 2)
    }, now);

    private static PetState Interact(PetState state, DateTimeOffset now) => state with
    {
        LastInteractionUtc = now,
        LastUpdatedUtc = now
    };

    private static double Clamp(double value) => Math.Clamp(value, 0, 100);
}

