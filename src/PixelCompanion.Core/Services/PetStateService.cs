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
        var sleeping = state.Activity == ActivityState.Sleeping;

        return UpdateMood(state with
        {
            Hunger = Clamp(state.Hunger + 2.2 * hours),
            Cleanliness = Clamp(state.Cleanliness - 0.9 * hours),
            Happiness = Clamp(state.Happiness - (sleeping ? 0.15 : 0.45) * hours),
            Fatigue = Clamp(state.Fatigue + (sleeping ? -12 : 1.4) * hours),
            Affection = Clamp(state.Affection - 0.08 * hours),
            LastUpdatedUtc = now
        });
    }

    public PetState Feed(PetState state, DateTimeOffset now) => UpdateMood(Interact(state with
    {
        Hunger = Clamp(state.Hunger - 28), Happiness = Clamp(state.Happiness + 4),
        Activity = ActivityState.Idle
    }, now));

    public PetState Pet(PetState state, DateTimeOffset now) => UpdateMood(Interact(state with
    {
        Happiness = Clamp(state.Happiness + 8), Affection = Clamp(state.Affection + 2)
    }, now));

    public PetState Play(PetState state, DateTimeOffset now) => UpdateMood(Interact(state with
    {
        Happiness = Clamp(state.Happiness + 14), Fatigue = Clamp(state.Fatigue + 8),
        Hunger = Clamp(state.Hunger + 4), Activity = ActivityState.Playing
    }, now));

    public PetState Clean(PetState state, DateTimeOffset now) => UpdateMood(Interact(state with
    {
        Cleanliness = Clamp(state.Cleanliness + 35), Happiness = Clamp(state.Happiness + 2),
        Activity = ActivityState.Cleaning
    }, now));

    public PetState Sleep(PetState state, DateTimeOffset now) => UpdateMood(Interact(state with
    {
        Activity = ActivityState.Sleeping
    }, now));

    public PetState Wake(PetState state, DateTimeOffset now) => UpdateMood(Interact(state with
    {
        Activity = ActivityState.Idle,
        Fatigue = Clamp(state.Fatigue - 18),
        Happiness = Clamp(state.Happiness + 2)
    }, now));

    private static PetState Interact(PetState state, DateTimeOffset now) => state with
    {
        LastInteractionUtc = now,
        LastUpdatedUtc = now
    };

    private static double Clamp(double value) => Math.Clamp(value, 0, 100);

    private static PetState UpdateMood(PetState state)
    {
        var mood = state.Fatigue >= 75 ? MoodState.Tired :
            state.Hunger >= 75 ? MoodState.Hungry :
            state.Happiness <= 30 || state.Cleanliness <= 25 ? MoodState.Sad :
            state.Happiness >= 80 ? MoodState.Happy :
            MoodState.Neutral;
        return state with { Mood = mood };
    }
}
