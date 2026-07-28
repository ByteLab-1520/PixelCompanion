using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using PixelCompanion.Core.Models;

namespace PixelCompanion.Core.Services;

public sealed class GitHubReleaseUpdateService
{
    public static string InstallerAssetName => ProductEditionInfo.InstallerAssetName;
    public static string ChecksumAssetName => ProductEditionInfo.ChecksumAssetName;
    public static string SignatureMarkerAssetName => ProductEditionInfo.SignatureMarkerAssetName;
    public static readonly Uri LatestReleaseApi = new("https://api.github.com/repos/ByteLab-1520/PixelCompanion/releases/latest");
    private readonly HttpClient _client;

    public GitHubReleaseUpdateService(HttpClient? client = null)
    {
        _client = client ?? new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        if (!_client.DefaultRequestHeaders.UserAgent.Any())
            _client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("PixelCompanion", "1.0"));
        _client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
    }

    public async Task<UpdateCheckResult> CheckAsync(Version currentVersion, CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _client.GetAsync(LatestReleaseApi, cancellationToken);
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var release = await JsonSerializer.DeserializeAsync<GitHubRelease>(stream, cancellationToken: cancellationToken);
            if (release is null || !TryParseVersion(release.TagName, out var version))
                return new(false, Error: "The latest GitHub Release has an invalid version tag.");

            var assets = release.Assets ?? [];
            var installer = assets.FirstOrDefault(asset =>
                string.Equals(asset.Name, InstallerAssetName, StringComparison.Ordinal));
            if (installer is null || !Uri.TryCreate(installer.DownloadUrl, UriKind.Absolute, out var installerUri))
                return new(false, Error: $"The release does not contain {InstallerAssetName}.");

            var checksum = assets.FirstOrDefault(asset =>
                string.Equals(asset.Name, ChecksumAssetName, StringComparison.Ordinal));
            var checksumUri = checksum is not null && Uri.TryCreate(checksum.DownloadUrl, UriKind.Absolute, out var parsedChecksum)
                ? parsedChecksum
                : null;
            var digest = NormalizeDigest(installer.Digest);
            if (checksumUri is null && digest is null)
                return new(false, Error: "The release does not provide a SHA-256 checksum.");

            if (!Uri.TryCreate(release.HtmlUrl, UriKind.Absolute, out var releasePage))
                releasePage = new Uri("https://github.com/ByteLab-1520/PixelCompanion/releases");

            var supportsAutomaticInstall = assets.Any(asset =>
                string.Equals(asset.Name, SignatureMarkerAssetName, StringComparison.Ordinal));
            var info = new ReleaseUpdateInfo(
                version!,
                release.TagName!,
                releasePage,
                installerUri,
                checksumUri,
                digest,
                supportsAutomaticInstall);
            return new(version! > currentVersion, info);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            return new(false, Error: ex.Message);
        }
    }

    public static bool TryParseVersion(string? tagName, out Version? version)
    {
        if (string.IsNullOrWhiteSpace(tagName))
        {
            version = null;
            return false;
        }
        var value = tagName.Trim();
        if (value.StartsWith('v') || value.StartsWith('V')) value = value[1..];
        var separator = value.IndexOfAny(['-', '+']);
        if (separator >= 0) value = value[..separator];
        return Version.TryParse(value, out version);
    }

    public static string? NormalizeDigest(string? digest)
    {
        if (string.IsNullOrWhiteSpace(digest)) return null;
        var value = digest.Trim();
        if (value.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase)) value = value[7..];
        return value.Length == 64 && value.All(Uri.IsHexDigit) ? value.ToLowerInvariant() : null;
    }

    private sealed record GitHubRelease(
        [property: JsonPropertyName("tag_name")] string? TagName,
        [property: JsonPropertyName("html_url")] string? HtmlUrl,
        [property: JsonPropertyName("assets")] GitHubAsset[]? Assets);

    private sealed record GitHubAsset(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("browser_download_url")] string DownloadUrl,
        [property: JsonPropertyName("digest")] string? Digest);
}
