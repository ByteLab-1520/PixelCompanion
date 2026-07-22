using PixelCompanion.Core.Models;

namespace PixelCompanion.Core.Services;

public sealed class CharacterPackValidator
{
    private static readonly string[] RequiredAnimations = ["idle", "walk", "drag", "fall", "land"];

    public IReadOnlyList<PackValidationIssue> Validate(CharacterManifest manifest, AnimationCatalog catalog, string packRoot)
    {
        var issues = new List<PackValidationIssue>();
        if (string.IsNullOrWhiteSpace(manifest.Id)) issues.Add(Error("manifest.id", "Character ID is required."));
        if (manifest.PackFormatVersion != 1) issues.Add(Error("manifest.version", $"Unsupported pack format {manifest.PackFormatVersion}."));
        if (manifest.CanvasWidth <= 0 || manifest.CanvasHeight <= 0) issues.Add(Error("manifest.canvas", "Canvas size must be positive."));

        foreach (var id in RequiredAnimations)
            if (!catalog.Animations.ContainsKey(id)) issues.Add(Error("animation.required", $"Required animation '{id}' is missing."));

        foreach (var animation in catalog.Animations.Values)
        {
            if (animation.Frames.Count == 0)
                issues.Add(Error("animation.frames", $"Animation '{animation.Id}' has no frames."));
            foreach (var frame in animation.Frames)
            {
                var fullPath = Path.GetFullPath(Path.Combine(packRoot, frame.Source));
                var relative = Path.GetRelativePath(Path.GetFullPath(packRoot), fullPath);
                if (relative == ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                    issues.Add(Error("animation.path", $"Frame path escapes the character pack: {frame.Source}"));
                else if (!File.Exists(fullPath))
                    issues.Add(Error("animation.file", $"Frame file is missing: {frame.Source}"));
                if (frame.DurationMs is < 16 or > 60_000)
                    issues.Add(Error("animation.duration", $"Frame duration is invalid: {frame.DurationMs}ms"));
            }
        }

        return issues;
    }

    private static PackValidationIssue Error(string code, string message) => new(code, message, true);
}
