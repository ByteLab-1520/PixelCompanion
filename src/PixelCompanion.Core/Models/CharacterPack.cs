using System.Text.Json.Serialization;

namespace PixelCompanion.Core.Models;

public sealed record CharacterManifest
{
    public required string Id { get; init; }
    public Dictionary<string, string> DisplayName { get; init; } = [];
    public Dictionary<string, string> Description { get; init; } = [];
    public string Author { get; init; } = "Unknown";
    public string Version { get; init; } = "1.0.0";
    public int PackFormatVersion { get; init; } = 1;
    public string DefaultLanguage { get; init; } = "en";
    public string[] SupportedLanguages { get; init; } = ["en"];
    public int CanvasWidth { get; init; } = 96;
    public int CanvasHeight { get; init; } = 96;
    public string License { get; init; } = "CC0-1.0";
}

public sealed record AnimationCatalog
{
    public Dictionary<string, AnimationDefinition> Animations { get; init; } = [];
}

public sealed record AnimationDefinition
{
    public required string Id { get; init; }
    public List<AnimationFrame> Frames { get; init; } = [];
    public bool Loop { get; init; } = true;
    public bool CanFlipHorizontally { get; init; } = true;
    public string? TransitionAfter { get; init; }
    public Anchor GroundAnchor { get; init; } = new(0.5, 1);
    public Anchor DragAnchor { get; init; } = new(0.5, 0.25);
    public Dictionary<string, Anchor> PropAnchors { get; init; } = [];
}

public sealed record AnimationFrame
{
    public required string Source { get; init; }
    public int DurationMs { get; init; } = 250;
}

public sealed record Anchor(double X, double Y);

public sealed record PackValidationIssue(string Code, string Message, bool IsError);

