namespace PixelCompanion.Core.Models;

public enum CharacterImageSlot
{
    Default = 0,
    Back = 1,
    Walk1 = 2,
    Walk2 = 3,
    Walk3 = 4,
    Eat1 = 5,
    Eat2 = 6,
    Sleep1 = 7,
    Sleep2 = 8,

    [Obsolete("Use Walk1. This alias is retained for v0.2 profile compatibility.")]
    WalkLeft = Walk1,
    [Obsolete("Use Walk2. This alias is retained for v0.2 profile compatibility.")]
    WalkRight = Walk2,
    [Obsolete("Use Walk3. This alias is retained for v0.2 profile compatibility.")]
    WalkMiddle = Walk3
}

public static class CharacterImageSlots
{
    public static IReadOnlyList<CharacterImageSlot> All { get; } =
    [
        CharacterImageSlot.Default,
        CharacterImageSlot.Back,
        CharacterImageSlot.Walk1,
        CharacterImageSlot.Walk2,
        CharacterImageSlot.Walk3,
        CharacterImageSlot.Eat1,
        CharacterImageSlot.Eat2,
        CharacterImageSlot.Sleep1,
        CharacterImageSlot.Sleep2
    ];
}

public sealed record UserCharacterProfile
{
    public int SchemaVersion { get; init; } = 2;
    public Dictionary<CharacterImageSlot, string> Images { get; init; } = [];
    public DateTimeOffset UpdatedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public string? GetImage(CharacterImageSlot slot) =>
        Images.TryGetValue(slot, out var relativePath) ? relativePath : null;
}

public sealed record CharacterImageImportResult(
    bool Success,
    string? RelativePath = null,
    string? Error = null);
