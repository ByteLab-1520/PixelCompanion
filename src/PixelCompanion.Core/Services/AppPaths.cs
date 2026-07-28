namespace PixelCompanion.Core.Services;

public sealed class AppPaths
{
    private readonly bool _canMigrateLegacyYaroroData;

    public AppPaths(string? overrideRoot = null)
    {
        var environmentRoot = Environment.GetEnvironmentVariable("PIXELCOMPANION_DATA_DIR");
        Root = overrideRoot ??
               (!string.IsNullOrWhiteSpace(environmentRoot) ? environmentRoot : ResolveUserDataRoot());
        _canMigrateLegacyYaroroData = ProductEditionInfo.IsYaroro &&
                                     overrideRoot is null &&
                                     string.IsNullOrWhiteSpace(environmentRoot);
        SettingsFile = Path.Combine(Root, "settings.json");
        RegionsFile = Path.Combine(Root, "regions.json");
        PetStateFile = Path.Combine(Root, "pet-state.json");
        TimersFile = Path.Combine(Root, "timers.json");
        Characters = Path.Combine(Root, "characters");
        Locales = Path.Combine(Root, "locales");
        Backups = Path.Combine(Root, "backups");
        Logs = Path.Combine(Root, "logs");
        Dialogues = Path.Combine(Root, "dialogues");
        UserCharacter = Path.Combine(Characters, "UserCharacter");
        UserCharacterImages = Path.Combine(UserCharacter, "images");
        UserCharacterProfileFile = Path.Combine(UserCharacter, "character-images.json");
        Updates = Path.Combine(Root, "updates");
        UpdateStateFile = Path.Combine(Updates, "update-state.json");
    }

    public string Root { get; }
    public string SettingsFile { get; }
    public string RegionsFile { get; }
    public string PetStateFile { get; }
    public string TimersFile { get; }
    public string Characters { get; }
    public string Locales { get; }
    public string Backups { get; }
    public string Logs { get; }
    public string Dialogues { get; }
    public string UserCharacter { get; }
    public string UserCharacterImages { get; }
    public string UserCharacterProfileFile { get; }
    public string Updates { get; }
    public string UpdateStateFile { get; }

    public void EnsureCreated()
    {
        if (_canMigrateLegacyYaroroData && !Directory.Exists(Root))
            MigrateLegacyYaroroData();

        foreach (var path in new[] { Root, Characters, Locales, Backups, Logs, Dialogues, UserCharacter, UserCharacterImages, Updates })
            Directory.CreateDirectory(path);
    }

    public string GetDialogueFile(string characterId, string language)
    {
        if (!IsSafeIdentifier(characterId) || !IsSafeIdentifier(language))
            throw new ArgumentException("Dialogue character and language IDs must use letters, digits, dot, dash, or underscore.");
        return Path.Combine(Dialogues, characterId, language + ".json");
    }

    private static string ResolveUserDataRoot()
    {
        if (OperatingSystem.IsMacOS())
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Library",
                "Application Support",
                ProductEditionInfo.DataDirectoryName);

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            ProductEditionInfo.DataDirectoryName);
    }

    private void MigrateLegacyYaroroData()
    {
        var legacy = OperatingSystem.IsMacOS()
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Library",
                "Application Support",
                "PixelCompanion")
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PixelCompanion");
        if (!Directory.Exists(legacy) || Path.GetFullPath(legacy).Equals(Path.GetFullPath(Root), StringComparison.OrdinalIgnoreCase))
            return;

        CopyDirectory(legacy, Root);
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(Path.Combine(destination, Path.GetRelativePath(source, directory)));
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            if (file.EndsWith(".lock", StringComparison.OrdinalIgnoreCase)) continue;
            var target = Path.Combine(destination, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: false);
        }
    }

    private static bool IsSafeIdentifier(string value) =>
        !string.IsNullOrWhiteSpace(value) &&
        value.All(character => char.IsAsciiLetterOrDigit(character) || character is '.' or '-' or '_');
}
