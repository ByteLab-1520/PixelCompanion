namespace PixelCompanion.Core.Services;

public sealed class AppPaths
{
    public AppPaths(string? overrideRoot = null)
    {
        Root = overrideRoot ?? ResolveUserDataRoot();
        SettingsFile = Path.Combine(Root, "settings.json");
        RegionsFile = Path.Combine(Root, "regions.json");
        PetStateFile = Path.Combine(Root, "pet-state.json");
        TimersFile = Path.Combine(Root, "timers.json");
        Characters = Path.Combine(Root, "characters");
        Locales = Path.Combine(Root, "locales");
        Backups = Path.Combine(Root, "backups");
        Logs = Path.Combine(Root, "logs");
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

    public void EnsureCreated()
    {
        foreach (var path in new[] { Root, Characters, Locales, Backups, Logs })
            Directory.CreateDirectory(path);
    }

    private static string ResolveUserDataRoot()
    {
        if (OperatingSystem.IsMacOS())
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Application Support", "PixelCompanion");

        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PixelCompanion");
    }
}

