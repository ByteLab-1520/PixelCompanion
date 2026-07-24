namespace PixelCompanion.Core.Models;

public enum CharacterImageSlot
{
    Default,
    Back,
    WalkLeft,
    WalkRight,
    WalkMiddle
}

public sealed record UserCharacterProfile
{
    public int SchemaVersion { get; init; } = 1;
    public Dictionary<CharacterImageSlot, string> Images { get; init; } = [];
    public DateTimeOffset UpdatedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public string? GetImage(CharacterImageSlot slot) =>
        Images.TryGetValue(slot, out var relativePath) ? relativePath : null;
}

public sealed record CharacterImageImportResult(
    bool Success,
    string? RelativePath = null,
    string? Error = null);
