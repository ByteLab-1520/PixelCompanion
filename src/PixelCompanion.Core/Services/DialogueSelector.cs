namespace PixelCompanion.Core.Services;

public sealed record DialogueLine(string Id, string Text, double Probability = 1, double MinimumAffection = 0, int CooldownSeconds = 0);

public sealed class DialogueSelector(int recentExclusionCount = 2, Random? random = null)
{
    private readonly Queue<string> _recent = new();
    private readonly Dictionary<string, DateTimeOffset> _lastUsed = [];
    private readonly Random _random = random ?? Random.Shared;

    public DialogueLine? Select(IEnumerable<DialogueLine> lines, double affection, DateTimeOffset now)
    {
        var candidates = lines.Where(line =>
            affection >= line.MinimumAffection &&
            !_recent.Contains(line.Id) &&
            (!_lastUsed.TryGetValue(line.Id, out var last) || now - last >= TimeSpan.FromSeconds(line.CooldownSeconds)) &&
            _random.NextDouble() <= line.Probability).ToArray();

        if (candidates.Length == 0)
            candidates = lines.Where(line => affection >= line.MinimumAffection && !_recent.Contains(line.Id)).ToArray();
        if (candidates.Length == 0) return null;

        var selected = candidates[_random.Next(candidates.Length)];
        _recent.Enqueue(selected.Id);
        while (_recent.Count > recentExclusionCount) _recent.Dequeue();
        _lastUsed[selected.Id] = now;
        return selected;
    }
}

