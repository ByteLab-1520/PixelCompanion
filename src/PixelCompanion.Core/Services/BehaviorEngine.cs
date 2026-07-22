using PixelCompanion.Core.Models;

namespace PixelCompanion.Core.Services;

public sealed record BehaviorDecision(ActivityState Activity, MoodState Mood, string Reason, int Priority);

public sealed class BehaviorEngine
{
    public BehaviorDecision Decide(PetState pet, EnvironmentSnapshot environment)
    {
        if (environment.IsDragging) return New(ActivityState.BeingDragged, pet.Mood, "dragging", 1000);
        if (environment.IsHidden) return New(ActivityState.Idle, pet.Mood, "hidden", 950);
        if (environment.IsOutsideRegion) return New(ActivityState.Walking, pet.Mood, "region-recovery", 900);
        if (environment.HasBattery && environment.BatteryPercent is <= 8)
            return New(ActivityState.Sitting, MoodState.Tired, "critical-battery", 850);
        if (environment.DoNotDisturb || environment.IsFullScreen)
            return New(ActivityState.Sitting, pet.Mood, "do-not-disturb", 800);
        if (environment.DirectCommandPending) return New(ActivityState.Playing, MoodState.Happy, "direct-command", 700);
        if (environment.IsMediaPlaying)
            return New(environment.IsVideo ? ActivityState.WatchingVideo : ActivityState.ListeningToMusic, MoodState.Happy, "media", 600);
        if (environment.IsCharging) return New(ActivityState.Charging, pet.Mood, "charging", 575);
        if (environment.IdleTime >= TimeSpan.FromMinutes(15)) return New(ActivityState.Sleeping, MoodState.Tired, "user-away", 500);
        if (pet.Fatigue >= 80) return New(ActivityState.Sleeping, MoodState.Tired, "fatigue", 450);
        if (pet.Hunger >= 75) return New(ActivityState.Sitting, MoodState.Hungry, "hunger", 400);
        if (pet.Happiness <= 25) return New(ActivityState.Idle, MoodState.Sad, "low-happiness", 350);
        return New(ActivityState.Idle, pet.Happiness >= 80 ? MoodState.Happy : MoodState.Neutral, "ambient", 100);
    }

    private static BehaviorDecision New(ActivityState activity, MoodState mood, string reason, int priority) => new(activity, mood, reason, priority);
}

