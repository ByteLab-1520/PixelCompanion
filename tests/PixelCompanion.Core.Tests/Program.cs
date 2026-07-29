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
    ("character dialogue files round-trip, validate, and recover", TestCharacterDialogues),
    ("bundled character pack is valid", TestBundledPack),
    ("bundled character action sprites are complete", TestBundledActionSprites),
    ("character images validate contents and persist slots", TestCharacterImages),
    ("legacy walking slot names remain compatible", TestLegacyCharacterSlots),
    ("GitHub release update metadata is parsed safely", TestReleaseUpdate),
    ("locale JSON files are valid", TestLocaleFiles),
    ("window and desktop surface placement stays in bounds", TestMovementGeometry),
    ("care interactions and assistant timers remain bounded", TestCareAndTimers),
    ("v0.2 settings remain compatible with v0.3 defaults", TestSettingsCompatibility),
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
    var conditional = new[]
    {
        new DialogueLine("hungry", "Food?", MinimumHunger: 70),
        new DialogueLine("tired", "Sleep?", MinimumFatigue: 70)
    };
    var hungry = new DialogueSelector(0, new Random(1)).Select(
        conditional,
        new PetState { Hunger = 90, Fatigue = 10 },
        DateTimeOffset.Now);
    Assert(hungry?.Id == "hungry", "state-conditioned dialogue selection failed");
    return Task.CompletedTask;
}

static async Task TestCharacterDialogues()
{
    var root = Path.Combine(Path.GetTempPath(), "PixelCompanionTests", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(root);
    try
    {
        var paths = new AppPaths(root);
        var service = new CharacterDialogueService(paths, new AtomicJsonStore());
        var catalog = CharacterDialogueService.CreateDefaults("test-character", "ko", key => key);
        await service.SaveAsync(catalog);
        await service.SaveAsync(catalog with
        {
            Groups = catalog.Groups.ToDictionary(
                pair => pair.Key,
                pair => pair.Key == DialogueGroupIds.Click
                    ? new List<DialogueLine> { new("custom.1", "직접 쓴 대사") }
                    : pair.Value,
                StringComparer.Ordinal)
        });

        var loaded = await service.LoadAsync("test-character", "ko", () => catalog);
        Assert(loaded.GetGroup(DialogueGroupIds.Click).Single().Text == "직접 쓴 대사", "dialogue round trip failed");
        Assert(File.Exists(paths.GetDialogueFile("test-character", "ko") + ".bak"), "dialogue backup was not created");

        var yaroroDefaults = CharacterDialogueService.CreateDefaults("yaroro", "ko", key => key);
        Assert(
            yaroroDefaults.GetGroup(DialogueGroupIds.Click).Single().Text == "안녕? 야로로대장이야",
            "Yaroro's Korean greeting must contain only the captain introduction");
        var legacyYaroro = yaroroDefaults with
        {
            Groups = yaroroDefaults.Groups.ToDictionary(
                pair => pair.Key,
                pair => pair.Key == DialogueGroupIds.Click
                    ?
                    [
                        new DialogueLine("click.1", "안녕! 옆에서 조용히 함께하고 있었어."),
                        new DialogueLine("click.2", "오늘 하는 일도 잘 풀리면 좋겠다."),
                        new DialogueLine("custom.1", "사용자가 추가한 인사")
                    ]
                    : pair.Value,
                StringComparer.Ordinal)
        };
        await service.SaveAsync(legacyYaroro);
        var migratedYaroro = await service.LoadAsync("yaroro", "ko", () => yaroroDefaults);
        var migratedGreetings = migratedYaroro.GetGroup(DialogueGroupIds.Click);
        Assert(migratedGreetings.Count == 2, "Yaroro's legacy default greeting was not removed");
        Assert(
            migratedGreetings[0].Text == "안녕? 야로로대장이야" &&
            migratedGreetings[1].Text == "사용자가 추가한 인사",
            "Yaroro's greeting migration did not preserve the custom line");

        var invalid = loaded with
        {
            Groups = new Dictionary<string, List<DialogueLine>>
            {
                [DialogueGroupIds.Click] = [new("bad", "", Probability: 2)]
            }
        };
        Assert(CharacterDialogueService.Validate(invalid).Count >= 2, "invalid dialogue data was accepted");
    }
    finally { Directory.Delete(root, true); }
}

static async Task TestBundledPack()
{
    var root = Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", "..", "assets", "characters",
        ProductEditionInfo.DefaultCharacterFolder));
    var options = new JsonSerializerOptions(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true };
    var manifest = JsonSerializer.Deserialize<CharacterManifest>(await File.ReadAllTextAsync(Path.Combine(root, "character.json")), options)!;
    var catalog = JsonSerializer.Deserialize<AnimationCatalog>(await File.ReadAllTextAsync(Path.Combine(root, "animations.json")), options)!;
    var issues = new CharacterPackValidator().Validate(manifest, catalog, root);
    Assert(issues.Count == 0, string.Join("; ", issues.Select(x => x.Message)));
}

static async Task TestBundledActionSprites()
{
    var root = Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", "..", "assets", "characters",
        ProductEditionInfo.DefaultCharacterFolder, "sprites"));
    var prefix = ProductEditionInfo.IsYaroro ? "yaroro" : "default-cat";
    var files = new List<string>
    {
        ProductEditionInfo.IsYaroro ? "yaroro-default.png" : "default-cat.png",
        $"{prefix}-back.png",
        $"{prefix}-walk-1.png",
        $"{prefix}-walk-2.png",
        $"{prefix}-walk-3.png",
        $"{prefix}-eat-1.png",
        $"{prefix}-eat-2.png",
        $"{prefix}-sleep-1.png",
        $"{prefix}-sleep-2.png"
    };
#if PIXELCOMPANION_YARORO
    files.Add("yaroro-drag-propeller.png");
    files.Add("yaroro-fall.png");
    files.Add("yaroro-land-stunned.png");
#endif
    var expectedSize = ProductEditionInfo.IsYaroro ? 418 : 128;
    foreach (var file in files)
    {
        var bytes = await File.ReadAllBytesAsync(Path.Combine(root, file));
        Assert(bytes.Length > 32 && bytes.AsSpan(1, 3).SequenceEqual("PNG"u8), $"{file} is not a PNG");
        var width = System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(16, 4));
        var height = System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(20, 4));
        Assert(width == expectedSize && height == expectedSize, $"{file} must use a {expectedSize}x{expectedSize} canvas");
        Assert(bytes[25] is 4 or 6, $"{file} must contain an alpha channel");
    }
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
        var imported = await service.ImportAsync(CharacterImageSlot.Walk1, validImage);
        Assert(imported.Success, imported.Error ?? "valid image was rejected");
        var profile = await service.LoadAsync();
        Assert(service.ResolvePath(profile, CharacterImageSlot.Walk1) is not null, "slot was not persisted");
        var rejected = await service.ImportAsync(CharacterImageSlot.Default, fakeImage);
        Assert(!rejected.Success, "a fake JPG was accepted");
    }
    finally { Directory.Delete(root, true); }
}

static Task TestLegacyCharacterSlots()
{
    const string legacyJson = """{"schemaVersion":1,"images":{"WalkLeft":"images/walk-left.png","WalkRight":"images/walk-right.png","WalkMiddle":"images/walk-middle.png"}}""";
    var profile = JsonSerializer.Deserialize<UserCharacterProfile>(
        legacyJson,
        new JsonSerializerOptions(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true });
    Assert(profile is not null, "legacy profile could not be read");
    Assert(profile!.Images.ContainsKey(CharacterImageSlot.Walk1), "WalkLeft was not migrated to Walk1");
    Assert(profile.Images.ContainsKey(CharacterImageSlot.Walk2), "WalkRight was not migrated to Walk2");
    Assert(profile.Images.ContainsKey(CharacterImageSlot.Walk3), "WalkMiddle was not migrated to Walk3");
    Assert(CharacterImageSlots.All.Count == 12 && CharacterImageSlots.All.Distinct().Count() == 12, "slot catalog contains duplicates");
    return Task.CompletedTask;
}

static async Task TestReleaseUpdate()
{
    const string hash = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    var installerName = ProductEditionInfo.InstallerAssetName;
    var markerName = ProductEditionInfo.SignatureMarkerAssetName;
    var json = $$"""
        {
          "tag_name": "v0.2.0",
          "html_url": "https://github.com/ByteLab-1520/PixelCompanion/releases/tag/v0.2.0",
          "assets": [
            {
              "name": "{{installerName}}",
              "browser_download_url": "https://example.test/{{installerName}}",
              "digest": "sha256:{{hash}}"
            },
            {
              "name": "{{markerName}}",
              "browser_download_url": "https://example.test/{{markerName}}",
              "digest": null
            }
          ]
        }
        """;
    using var client = new HttpClient(new StubHttpHandler(json));
    var result = await new GitHubReleaseUpdateService(client).CheckAsync(new Version(0, 1, 0));
    var release = result.Release;
    Assert(result.IsUpdateAvailable && release?.Version == new Version(0, 2, 0), "new version was not detected");
    Assert(release?.AssetSha256 == hash, "asset digest was not normalized");
    Assert(release?.SupportsAutomaticInstall == true, "signed release marker was not detected");

    var unsignedJson = json.Replace(
        markerName,
        "UNSIGNED_INSTALLER.txt",
        StringComparison.Ordinal);
    using var unsignedClient = new HttpClient(new StubHttpHandler(unsignedJson));
    var unsignedResult = await new GitHubReleaseUpdateService(unsignedClient).CheckAsync(new Version(0, 1, 0));
    Assert(unsignedResult.Release?.SupportsAutomaticInstall == false, "unsigned release enabled automatic install");
}

static async Task TestLocaleFiles()
{
    var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "assets", "locales"));
    HashSet<string>? englishKeys = null;
    foreach (var name in new[] { "en.json", "ko.json" })
    {
        await using var stream = File.OpenRead(Path.Combine(root, name));
        using var document = await JsonDocument.ParseAsync(stream);
        Assert(document.RootElement.ValueKind == JsonValueKind.Object, $"{name} is not a JSON object");
        var keys = document.RootElement.EnumerateObject().Select(property => property.Name).ToHashSet(StringComparer.Ordinal);
        if (name == "en.json") englishKeys = keys;
        else Assert(englishKeys is not null && keys.SetEquals(englishKeys), $"{name} locale keys do not match en.json");
    }
}

static Task TestMovementGeometry()
{
    var window = new MovementSurface(
        "window:1",
        MovementSurfaceKind.WindowTop,
        new DesktopRect(100, 300, 500, 400));
    Assert(MovementGeometry.TryPlace(window, 999, 120, 160, out var placement), "valid window was rejected");
    Assert(placement.X == 480, "placement was not clamped to the window width");
    Assert(placement.Y == 140, "pet was not placed on the window top");

    var desktop = new MovementSurface(
        "desktop:1",
        MovementSurfaceKind.DesktopFloor,
        new DesktopRect(-1920, 0, 1920, 1040));
    Assert(MovementGeometry.TryPlace(desktop, -2000, 120, 160, out var desktopPlacement), "valid desktop was rejected");
    Assert(desktopPlacement.X == -1920 && desktopPlacement.Y == 880, "desktop placement is incorrect");
    Assert(MovementGeometry.HorizontalScale(movingLeft: true, frameFacesLeft: true) == 1,
        "left-facing frame should not flip while moving left");
    Assert(MovementGeometry.HorizontalScale(movingLeft: false, frameFacesLeft: true) == -1,
        "left-facing frame should flip while moving right");
    Assert(MovementGeometry.HorizontalScale(movingLeft: false, frameFacesLeft: false) == 1,
        "right-facing frame should not flip while moving right");
    Assert(MovementGeometry.HorizontalScale(movingLeft: true, frameFacesLeft: false) == -1,
        "right-facing frame should flip while moving left");
    Assert(MovementGeometry.ShouldSynchronizeAttachedSurface(isDragging: false),
        "an attached idle pet should continue following its surface");
    Assert(!MovementGeometry.ShouldSynchronizeAttachedSurface(isDragging: true),
        "surface tracking must not pull a dragged pet back onto a window");
    Assert(MovementGeometry.CanContinueLanding(7, 7, isDragging: false),
        "the current landing operation should be allowed to continue");
    Assert(!MovementGeometry.CanContinueLanding(7, 8, isDragging: false),
        "an outdated landing operation must stop");
    Assert(!MovementGeometry.CanContinueLanding(8, 8, isDragging: true),
        "grabbing a falling pet must cancel landing");
    var draggedToDesktop = MovementGeometry.FindNearest(
        [window, desktop],
        new DesktopPoint(-960, desktop.Bounds.Bottom),
        petWidth: 120);
    Assert(draggedToDesktop?.Id == desktop.Id,
        "a pet dragged down from a window should select the desktop landing surface");
    var sameScreenDesktop = new MovementSurface(
        "desktop:2",
        MovementSurfaceKind.DesktopFloor,
        new DesktopRect(0, 0, 1920, 1040));
    var snappedWindow = MovementGeometry.FindDragTarget(
        [window, sameScreenDesktop],
        new DesktopPoint(250, 330),
        petWidth: 120,
        sourceSurfaceId: sameScreenDesktop.Id);
    Assert(snappedWindow?.Id == window.Id, "dragging near a title bar should preview that window");
    var detachedDesktop = MovementGeometry.FindDragTarget(
        [window, sameScreenDesktop],
        new DesktopPoint(250, 500),
        petWidth: 120,
        sourceSurfaceId: window.Id);
    Assert(detachedDesktop?.Id == sameScreenDesktop.Id, "dragging below the detach threshold should target the desktop");

    var support = new MovementSurface(
        "window:support",
        MovementSurfaceKind.WindowTop,
        new DesktopRect(100, 300, 800, 500),
        NativeHandle: 10,
        ZOrder: 5);
    var frontObstacle = new MovementSurface(
        "window:front",
        MovementSurfaceKind.WindowTop,
        new DesktopRect(400, 200, 220, 300),
        NativeHandle: 20,
        ZOrder: 2,
        IsWalkable: false);
    var behindWindow = new MovementSurface(
        "window:behind",
        MovementSurfaceKind.WindowTop,
        new DesktopRect(200, 200, 120, 300),
        NativeHandle: 30,
        ZOrder: 9);
    var ranges = MovementGeometry.GetWalkableRanges(
        support,
        [support, frontObstacle, behindWindow],
        petWidth: 100);
    Assert(ranges.Count == 2, "a front window should split the support window into two walkable ranges");
    Assert(ranges[0] == new WalkableRange(100, 298), "left collision wall was calculated incorrectly");
    Assert(ranges[1] == new WalkableRange(622, 800), "right collision wall was calculated incorrectly");
    Assert(MovementGeometry.FindContainingRange(ranges, 250) == ranges[0],
        "pet position was not kept in its current free range");
    Assert(MovementGeometry.FindNearestRange(ranges, 500) == ranges[1],
        "an overlapped pet was not moved to the nearest free side");

    var nonCovering = frontObstacle with
    {
        Id = "window:above",
        Bounds = new DesktopRect(400, 50, 220, 100)
    };
    var unobstructed = MovementGeometry.GetWalkableRanges(support, [support, nonCovering], 100);
    Assert(unobstructed.SequenceEqual([new WalkableRange(100, 800)]),
        "a window that does not cover the title-bar line became a false obstacle");
    return Task.CompletedTask;
}

static Task TestCareAndTimers()
{
    var now = DateTimeOffset.UtcNow;
    var service = new PetStateService();
    var state = new PetState { Hunger = 90, Cleanliness = 10, Happiness = 20, Fatigue = 80 };
    var fed = service.Feed(state, now);
    Assert(fed.Hunger == 62 && fed.LastInteractionUtc == now, "feeding did not update bounded care values");
    var cleaned = service.Clean(fed, now);
    Assert(cleaned.Cleanliness == 45, "cleaning did not update cleanliness");
    var sleeping = service.Sleep(cleaned, now.AddHours(-1));
    var rested = service.ApplyElapsed(sleeping, now);
    Assert(rested.Fatigue < sleeping.Fatigue, "sleeping did not reduce fatigue");

    var timers = new AssistantTimerService();
    var timer = timers.Start(AssistantTimerKind.Focus, 25, now);
    Assert(timer.IsRunning && timer.DurationMinutes == 25, "focus timer did not start");
    Assert(!timers.IsComplete(timer, now.AddMinutes(24)), "timer completed too early");
    Assert(timers.IsComplete(timer, now.AddMinutes(25)), "timer did not complete");
    Assert(!timers.Cancel().IsRunning, "timer cancellation failed");
    return Task.CompletedTask;
}

static Task TestSettingsCompatibility()
{
    const string oldJson = """{"schemaVersion":1,"language":"ko","movementSpeed":"Slow"}""";
    var settings = JsonSerializer.Deserialize<AppSettings>(
        oldJson,
        new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
        });
    Assert(settings is not null && settings.Language == "ko", "v0.2 settings were not read");
    Assert(settings!.MovementSurfaceMode == MovementSurfaceMode.DesktopAndWindows, "new surface default was not applied");
    Assert(settings.FullScreenBehavior == FullScreenBehavior.WaitAtEdge, "new full-screen default was not applied");
    return Task.CompletedTask;
}

static async Task TestReleaseVersionConsistency()
{
    var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    var props = await File.ReadAllTextAsync(Path.Combine(root, "Directory.Build.props"));
    var buildScript = await File.ReadAllTextAsync(Path.Combine(root, "scripts", "Build-WindowsInstaller.ps1"));
    var finalizeScript = await File.ReadAllTextAsync(Path.Combine(root, "scripts", "Finalize-WindowsRelease.ps1"));
    var smokeTestScript = await File.ReadAllTextAsync(Path.Combine(root, "scripts", "Test-WindowsInstaller.ps1"));
    var installer = await File.ReadAllTextAsync(Path.Combine(root, "packaging", "windows", "PixelCompanion.iss"));
    const string version = "0.5.2";
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
