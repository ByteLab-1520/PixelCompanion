using PixelCompanion.Core.Models;

namespace PixelCompanion.Core.Services;

public sealed record DialogueLine(
    string Id,
    string Text,
    double Probability = 1,
    double MinimumAffection = 0,
    int CooldownSeconds = 0,
    double? MinimumHunger = null,
    double? MinimumFatigue = null,
    double? MaximumHappiness = null,
    int? StartHour = null,
    int? EndHour = null)
{
    public override string ToString() => Text;
}

public sealed class DialogueSelector(int recentExclusionCount = 2, Random? random = null)
{
    private readonly Queue<string> _recent = new();
    private readonly Dictionary<string, DateTimeOffset> _lastUsed = [];
    private readonly Random _random = random ?? Random.Shared;

    public DialogueLine? Select(IEnumerable<DialogueLine> lines, double affection, DateTimeOffset now)
        => Select(lines, new PetState { Affection = affection }, now);

    public DialogueLine? Select(IEnumerable<DialogueLine> lines, PetState state, DateTimeOffset now)
    {
        var candidates = lines.Where(line =>
            MeetsConditions(line, state, now) &&
            !_recent.Contains(line.Id) &&
            (!_lastUsed.TryGetValue(line.Id, out var last) || now - last >= TimeSpan.FromSeconds(line.CooldownSeconds)) &&
            _random.NextDouble() <= line.Probability).ToArray();

        if (candidates.Length == 0)
            candidates = lines.Where(line => MeetsConditions(line, state, now) && !_recent.Contains(line.Id)).ToArray();
        if (candidates.Length == 0) return null;

        var selected = candidates[_random.Next(candidates.Length)];
        _recent.Enqueue(selected.Id);
        while (_recent.Count > recentExclusionCount) _recent.Dequeue();
        _lastUsed[selected.Id] = now;
        return selected;
    }

    private static bool MeetsConditions(DialogueLine line, PetState state, DateTimeOffset now)
    {
        if (state.Affection < line.MinimumAffection ||
            line.MinimumHunger is { } hunger && state.Hunger < hunger ||
            line.MinimumFatigue is { } fatigue && state.Fatigue < fatigue ||
            line.MaximumHappiness is { } happiness && state.Happiness > happiness)
            return false;
        if (line.StartHour is not { } start || line.EndHour is not { } end)
            return true;
        var hour = now.ToLocalTime().Hour;
        return start <= end ? hour >= start && hour <= end : hour >= start || hour <= end;
    }
}
