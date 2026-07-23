using PixelCompanion.Core.Models;
using PixelCompanion.Core.Services;
using System.Text.Json;
using System.Net;

var tests = new (string Name, Func<Task> Run)[]
{
    ("offline progress is capped and clamped", TestOfflineProgress),
    ("behavior priorities are deterministic", TestBehaviorPriorities),
    ("localization falls back to English then key", TestLocalizationFallback),
    ("atomic JSON store round-trips and recovers", TestAtomicStore),
    ("dialogue selection excludes recent lines", TestDialogueSelection),
    ("bundled character pack is valid", TestBundledPack),
    ("character images validate contents and persist slots", TestCharacterImages),
    ("GitHub release update metadata is parsed safely", TestReleaseUpdate),
    ("locale JSON files are valid", TestLocaleFiles),
    ("release versions stay consistent", TestReleaseVersionConsistency)
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

static async Task TestCharacterImages()
{
    var root = Path.Combine(Path.GetTempPath(), "PixelCompanionTests", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(root);
    try
    {
        var validImage = Path.Combine(root, "source.png");
        await File.WriteAllBytesAsync(validImage, [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]);
        var fakeImage = Path.Combine(root, "fake.jpg");
        await File.WriteAllTextAsync(fakeImage, "not an image");
        var paths = new AppPaths(Path.Combine(root, "data"));
        var service = new UserCharacterService(paths, new AtomicJsonStore());
        var imported = await service.ImportAsync(CharacterImageSlot.WalkLeft, validImage);
        Assert(imported.Success, imported.Error ?? "valid image was rejected");
        var profile = await service.LoadAsync();
        Assert(service.ResolvePath(profile, CharacterImageSlot.WalkLeft) is not null, "slot was not persisted");
        var rejected = await service.ImportAsync(CharacterImageSlot.Default, fakeImage);
        Assert(!rejected.Success, "a fake JPG was accepted");
    }
    finally { Directory.Delete(root, true); }
}

static async Task TestReleaseUpdate()
{
    const string hash = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    var json = $$"""
        {
          "tag_name": "v0.2.0",
          "html_url": "https://github.com/ByteLab-1520/PixelCompanion/releases/tag/v0.2.0",
          "assets": [
            {
              "name": "PixelCompanion-Installer.exe",
              "browser_download_url": "https://example.test/PixelCompanion-Installer.exe",
              "digest": "sha256:{{hash}}"
            }
          ]
        }
        """;
    using var client = new HttpClient(new StubHttpHandler(json));
    var result = await new GitHubReleaseUpdateService(client).CheckAsync(new Version(0, 1, 0));
    var release = result.Release;
    Assert(result.IsUpdateAvailable && release?.Version == new Version(0, 2, 0), "new version was not detected");
    Assert(release?.AssetSha256 == hash, "asset digest was not normalized");
}

static async Task TestLocaleFiles()
{
    var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "assets", "locales"));
    foreach (var name in new[] { "en.json", "ko.json" })
    {
        await using var stream = File.OpenRead(Path.Combine(root, name));
        using var document = await JsonDocument.ParseAsync(stream);
        Assert(document.RootElement.ValueKind == JsonValueKind.Object, $"{name} is not a JSON object");
    }
}

static async Task TestReleaseVersionConsistency()
{
    var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    var props = await File.ReadAllTextAsync(Path.Combine(root, "Directory.Build.props"));
    var buildScript = await File.ReadAllTextAsync(Path.Combine(root, "scripts", "Build-WindowsInstaller.ps1"));
    var finalizeScript = await File.ReadAllTextAsync(Path.Combine(root, "scripts", "Finalize-WindowsRelease.ps1"));
    var smokeTestScript = await File.ReadAllTextAsync(Path.Combine(root, "scripts", "Test-WindowsInstaller.ps1"));
    var installer = await File.ReadAllTextAsync(Path.Combine(root, "packaging", "windows", "PixelCompanion.iss"));
    const string version = "0.2.0";
    Assert(props.Contains($"<Version>{version}</Version>", StringComparison.Ordinal), "project version is inconsistent");
    Assert(buildScript.Contains($"$Version = '{version}'", StringComparison.Ordinal), "build script version is inconsistent");
    Assert(finalizeScript.Contains($"$Version = '{version}'", StringComparison.Ordinal), "finalize script version is inconsistent");
    Assert(smokeTestScript.Contains($"$Version = '{version}'", StringComparison.Ordinal), "smoke-test script version is inconsistent");
    Assert(installer.Contains($"MyAppVersion \"{version}\"", StringComparison.Ordinal), "Inno Setup version is inconsistent");
}

static void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

sealed class StubHttpHandler(string json) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
        Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json)
        });
}
