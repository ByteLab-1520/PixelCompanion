using PixelCompanion.Core.Services;

namespace PixelCompanion.Core.Models;

public static class DialogueGroupIds
{
    public const string Click = "click";
    public const string Feed = "feed";
    public const string Play = "play";
    public const string Sleep = "sleep";
    public static readonly string[] All = [Click, Feed, Play, Sleep];
}

public sealed record CharacterDialogueCatalog
{
    public int SchemaVersion { get; init; } = 1;
    public string CharacterId { get; init; } = ProductEditionInfo.DefaultCharacterId;
    public string Language { get; init; } = "en";
    public Dictionary<string, List<DialogueLine>> Groups { get; init; } = [];

    public IReadOnlyList<DialogueLine> GetGroup(string groupId) =>
        Groups.TryGetValue(groupId, out var lines) ? lines : [];
}
