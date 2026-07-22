using PixelCompanion.Core.Models;
using PixelCompanion.Core.Services;
using System.Text.Json;

var tests = new (string Name, Func<Task> Run)[]
{
    ("offline progress is capped and clamped", TestOfflineProgress),
    ("behavior priorities are deterministic", TestBehaviorPriorities),
    ("localization falls back to English then key", TestLocalizationFallback),
    ("atomic JSON store round-trips and recovers", TestAtomicStore),
    ("dialogue selection excludes recent lines", TestDialogueSelection),
    ("bundled character pack is valid", TestBundledPack)
};

var failures = new List<string>();
foreach (var test in tests)
{
    try { await test.Run(); Console.WriteLine($"PASS {test.Name}"); }
    catch (Exception ex) { failures.Add($"FAIL {test.Name}: {ex.Message}"); }
}
foreach (var failure in failures) Console.Error.WriteLine(failure);
return failures.Count == 0 ? 0 : 1;

static Task TestOfflineProgress()
{
    var start = DateTimeOffset.UtcNow.AddDays(-30);
    var state = new PetState { Hunger = 10, Cleanliness = 90, LastUpdatedUtc = start };
    var result = new PetStateService().ApplyElapsed(state, DateTimeOffset.UtcNow);
    Assert(result.Hunger is > 35 and < 40, "offline hunger must use the 12-hour cap");
    Assert(result.Cleanliness is >= 0 and <= 100, "values must remain clamped");
    return Task.CompletedTask;
}

static Task TestBehaviorPriorities()
{
    var engine = new BehaviorEngine();
    var pet = new PetState { Fatigue = 95, Hunger = 95 };
    var drag = engine.Decide(pet, new EnvironmentSnapshot { IsDragging = true, IsMediaPlaying = true });
    Assert(drag.Activity == ActivityState.BeingDragged && drag.Priority == 1000, "drag must win");
    var dnd = engine.Decide(pet, new EnvironmentSnapshot { DoNotDisturb = true, IsMediaPlaying = true });
    Assert(dnd.Reason == "do-not-disturb", "DND must win over media and needs");
    return Task.CompletedTask;
}

static Task TestLocalizationFallback()
{
    var missing = new List<string>();
    var service = new LocalizationService(new Dictionary<string, string> { ["known"] = "English" }, new Dictionary<string, string>(), missing.Add);
    Assert(service.Get("known") == "English", "must fall back to English");
    Assert(service.Get("missing") == "missing" && missing.SequenceEqual(new[] { "missing" }), "must return and log missing key");
    return Task.CompletedTask;
}

static async Task TestAtomicStore()
{
    var root = Path.Combine(Path.GetTempPath(), "PixelCompanionTests", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(root);
    try
    {
        var path = Path.Combine(root, "settings.json");
        var store = new AtomicJsonStore();
        await store.SaveAsync(path, new AppSettings { Language = "en" });
        await store.SaveAsync(path, new AppSettings { Language = "ko" });
        var loaded = await store.LoadOrCreateAsync(path, () => new AppSettings());
        Assert(loaded.Language == "ko", "round trip failed");
        await File.WriteAllTextAsync(path, "{broken");
        var recovered = await store.LoadOrCreateAsync(path, () => new AppSettings());
        Assert(recovered.Language == "en", "backup recovery failed");
    }
    finally { Directory.Delete(root, true); }
}

static Task TestDialogueSelection()
{
    var selector = new DialogueSelector(1, new Random(7));
    var lines = new[] { new DialogueLine("a", "A"), new DialogueLine("b", "B") };
    var first = selector.Select(lines, 100, DateTimeOffset.UtcNow);
    var second = selector.Select(lines, 100, DateTimeOffset.UtcNow);
    Assert(first is not null && second is not null && first.Id != second.Id, "recent line repeated");
    return Task.CompletedTask;
}

static async Task TestBundledPack()
{
    var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "assets", "characters", "DefaultCat"));
    var options = new JsonSerializerOptions(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true };
    var manifest = JsonSerializer.Deserialize<CharacterManifest>(await File.ReadAllTextAsync(Path.Combine(root, "character.json")), options)!;
    var catalog = JsonSerializer.Deserialize<AnimationCatalog>(await File.ReadAllTextAsync(Path.Combine(root, "animations.json")), options)!;
    var issues = new CharacterPackValidator().Validate(manifest, catalog, root);
    Assert(issues.Count == 0, string.Join("; ", issues.Select(x => x.Message)));
}

static void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}
