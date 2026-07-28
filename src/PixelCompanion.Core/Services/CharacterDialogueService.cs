using PixelCompanion.Core.Models;

namespace PixelCompanion.Core.Services;

public sealed class CharacterDialogueService(AppPaths paths, AtomicJsonStore store)
{
    public const int MaximumLinesPerGroup = 200;
    public const int MaximumTextLength = 500;
    public const int MaximumCooldownSeconds = 86_400;

    public Task<CharacterDialogueCatalog> LoadAsync(
        string characterId,
        string language,
        Func<CharacterDialogueCatalog> defaults,
        CancellationToken cancellationToken = default) =>
        store.LoadOrCreateAsync(paths.GetDialogueFile(characterId, language), defaults, cancellationToken);

    public async Task SaveAsync(CharacterDialogueCatalog catalog, CancellationToken cancellationToken = default)
    {
        var errors = Validate(catalog);
        if (errors.Count > 0)
            throw new InvalidDataException(string.Join(Environment.NewLine, errors));
        await store.SaveAsync(paths.GetDialogueFile(catalog.CharacterId, catalog.Language), catalog, cancellationToken);
    }

    public static IReadOnlyList<string> Validate(CharacterDialogueCatalog catalog)
    {
        var errors = new List<string>();
        if (catalog.SchemaVersion != 1) errors.Add($"Unsupported dialogue schema {catalog.SchemaVersion}.");
        if (!IsSafeIdentifier(catalog.CharacterId)) errors.Add("Character ID is invalid.");
        if (!IsSafeIdentifier(catalog.Language)) errors.Add("Language ID is invalid.");

        foreach (var group in catalog.Groups)
        {
            if (!DialogueGroupIds.All.Contains(group.Key, StringComparer.Ordinal))
                errors.Add($"Dialogue group '{group.Key}' is not supported.");
            if (group.Value.Count > MaximumLinesPerGroup)
                errors.Add($"Dialogue group '{group.Key}' exceeds {MaximumLinesPerGroup} lines.");

            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var line in group.Value)
            {
                if (!IsSafeIdentifier(line.Id)) errors.Add($"Dialogue ID '{line.Id}' is invalid.");
                else if (!ids.Add(line.Id)) errors.Add($"Dialogue ID '{line.Id}' is duplicated in '{group.Key}'.");
                if (string.IsNullOrWhiteSpace(line.Text)) errors.Add($"Dialogue '{line.Id}' is empty.");
                else if (line.Text.Length > MaximumTextLength)
                    errors.Add($"Dialogue '{line.Id}' exceeds {MaximumTextLength} characters.");
                if (line.Probability is < 0 or > 1) errors.Add($"Dialogue '{line.Id}' probability must be between 0 and 1.");
                if (line.MinimumAffection is < 0 or > 100)
                    errors.Add($"Dialogue '{line.Id}' minimum affection must be between 0 and 100.");
                if (line.CooldownSeconds is < 0 or > MaximumCooldownSeconds)
                    errors.Add($"Dialogue '{line.Id}' cooldown is outside the supported range.");
            }
        }
        return errors;
    }

    public static CharacterDialogueCatalog CreateDefaults(
        string characterId,
        string language,
        Func<string, string> getText) =>
        new()
        {
            CharacterId = characterId,
            Language = language,
            Groups = new Dictionary<string, List<DialogueLine>>(StringComparer.Ordinal)
            {
                [DialogueGroupIds.Click] =
                [
                    new DialogueLine("click.1", getText("dialogue.click.1")),
                    new DialogueLine("click.2", getText("dialogue.click.2"))
                ],
                [DialogueGroupIds.Feed] =
                [
                    new DialogueLine("feed.1", getText("dialogue.feed"))
                ],
                [DialogueGroupIds.Play] =
                [
                    new DialogueLine("play.1", getText("dialogue.play"))
                ],
                [DialogueGroupIds.Sleep] =
                [
                    new DialogueLine("sleep.1", getText("dialogue.sleep"))
                ]
            }
        };

    private static bool IsSafeIdentifier(string value) =>
        !string.IsNullOrWhiteSpace(value) &&
        value.All(character => char.IsAsciiLetterOrDigit(character) || character is '.' or '-' or '_');
}
