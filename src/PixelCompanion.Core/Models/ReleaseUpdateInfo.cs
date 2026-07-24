namespace PixelCompanion.Core.Models;

public sealed record ReleaseUpdateInfo(
    Version Version,
    string TagName,
    Uri ReleasePage,
    Uri InstallerDownload,
    Uri? ChecksumDownload,
    string? AssetSha256,
    bool SupportsAutomaticInstall);

public sealed record UpdateCheckResult(
    bool IsUpdateAvailable,
    ReleaseUpdateInfo? Release = null,
    string? Error = null);

public sealed record UpdateState
{
    public DateTimeOffset? LastCheckedAtUtc { get; init; }
    public string? LatestTag { get; init; }
}
