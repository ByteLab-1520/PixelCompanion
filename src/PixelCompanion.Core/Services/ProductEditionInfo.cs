namespace PixelCompanion.Core.Services;

public static class ProductEditionInfo
{
#if PIXELCOMPANION_YARORO
    public const bool IsYaroro = true;
    public const string EditionId = "yaroro";
    public const string DisplayName = "Pixel Companion for Yaroro";
    public const string DataDirectoryName = "PixelCompanion-Yaroro";
    public const string InstallDirectoryName = "PixelCompanion-Yaroro";
    public const string DesktopExecutableName = "PixelCompanion.Yaroro.exe";
    public const string ConfigExecutableName = "PixelCompanion.Yaroro.Config.exe";
    public const string UpdaterExecutableName = "PixelCompanion.Yaroro.Updater.exe";
    public const string DesktopAssemblyName = "PixelCompanion.Yaroro";
    public const string DefaultCharacterId = "yaroro";
    public const string DefaultCharacterFolder = "Yaroro";
    public const bool DefaultFrameFacesLeft = true;
    public const string AutoStartValueName = "PixelCompanionYaroro";
    public const string InstallerAssetName = "PixelCompanion-Yaroro-Installer.exe";
#else
    public const bool IsYaroro = false;
    public const string EditionId = "standard";
    public const string DisplayName = "Pixel Companion";
    public const string DataDirectoryName = "PixelCompanion";
    public const string InstallDirectoryName = "PixelCompanion";
    public const string DesktopExecutableName = "PixelCompanion.exe";
    public const string ConfigExecutableName = "PixelCompanion.Config.exe";
    public const string UpdaterExecutableName = "PixelCompanion.Updater.exe";
    public const string DesktopAssemblyName = "PixelCompanion";
    public const string DefaultCharacterId = "default-cat";
    public const string DefaultCharacterFolder = "DefaultCat";
    public const bool DefaultFrameFacesLeft = false;
    public const string AutoStartValueName = "PixelCompanion";
    public const string InstallerAssetName = "PixelCompanion-Installer.exe";
#endif

    public const string ReleaseRepository = "ByteLab-1520/PixelCompanion";
    public static string ChecksumAssetName => InstallerAssetName + ".sha256";
    public static string SignatureMarkerAssetName => InstallerAssetName + ".authenticode.json";
    public static string LocalizeDisplayName(string localizedBaseName) =>
        IsYaroro ? $"{localizedBaseName} for Yaroro" : localizedBaseName;
}
