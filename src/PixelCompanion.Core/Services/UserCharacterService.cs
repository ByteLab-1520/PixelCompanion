using PixelCompanion.Core.Models;

namespace PixelCompanion.Core.Services;

public sealed class UserCharacterService(AppPaths paths, AtomicJsonStore store)
{
    public const long MaximumImageBytes = 20 * 1024 * 1024;
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".gif"
    };

    public Task<UserCharacterProfile> LoadAsync(CancellationToken cancellationToken = default) =>
        store.LoadOrCreateAsync(paths.UserCharacterProfileFile, () => new UserCharacterProfile(), cancellationToken);

    public async Task<CharacterImageImportResult> ImportAsync(
        CharacterImageSlot slot,
        string sourcePath,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!File.Exists(sourcePath))
                return new(false, Error: "The selected file does not exist.");

            var sourceExtension = Path.GetExtension(sourcePath);
            if (!AllowedExtensions.Contains(sourceExtension))
                return new(false, Error: "Only PNG, JPG, JPEG, and GIF images are supported.");

            var fileInfo = new FileInfo(sourcePath);
            if (fileInfo.Length <= 0 || fileInfo.Length > MaximumImageBytes)
                return new(false, Error: "The image must be between 1 byte and 20 MB.");

            var canonicalExtension = await DetectImageExtensionAsync(sourcePath, cancellationToken);
            if (canonicalExtension is null)
                return new(false, Error: "The file contents are not a supported PNG, JPEG, or GIF image.");

            paths.EnsureCreated();
            var destinationName = $"{FileStems(slot)[0]}{canonicalExtension}";
            var destinationPath = Path.Combine(paths.UserCharacterImages, destinationName);
            var temporaryPath = destinationPath + ".tmp";

            await using (var source = File.Open(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            await using (var destination = File.Open(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await source.CopyToAsync(destination, cancellationToken);
                await destination.FlushAsync(cancellationToken);
            }

            File.Move(temporaryPath, destinationPath, true);
            DeleteOtherSlotFormats(slot, destinationPath);

            var relativePath = Path.GetRelativePath(paths.UserCharacter, destinationPath).Replace('\\', '/');
            var profile = await LoadAsync(cancellationToken);
            var images = new Dictionary<CharacterImageSlot, string>(profile.Images)
            {
                [slot] = relativePath
            };
            await store.SaveAsync(paths.UserCharacterProfileFile, profile with
            {
                Images = images,
                UpdatedAtUtc = DateTimeOffset.UtcNow
            }, cancellationToken);

            return new(true, relativePath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new(false, Error: ex.Message);
        }
    }

    public async Task RemoveAsync(CharacterImageSlot slot, CancellationToken cancellationToken = default)
    {
        var profile = await LoadAsync(cancellationToken);
        var images = new Dictionary<CharacterImageSlot, string>(profile.Images);
        images.Remove(slot);
        DeleteOtherSlotFormats(slot, keepPath: null);
        await store.SaveAsync(paths.UserCharacterProfileFile, profile with
        {
            Images = images,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        }, cancellationToken);
    }

    public string? ResolvePath(UserCharacterProfile profile, CharacterImageSlot slot)
    {
        var relativePath = profile.GetImage(slot);
        if (string.IsNullOrWhiteSpace(relativePath)) return null;

        var root = Path.GetFullPath(paths.UserCharacter) + Path.DirectorySeparatorChar;
        var candidate = Path.GetFullPath(Path.Combine(paths.UserCharacter, relativePath));
        return candidate.StartsWith(root, StringComparison.OrdinalIgnoreCase) && File.Exists(candidate)
            ? candidate
            : null;
    }

    private static async Task<string?> DetectImageExtensionAsync(string path, CancellationToken cancellationToken)
    {
        var header = new byte[8];
        await using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        var read = await stream.ReadAsync(header, cancellationToken);
        if (read >= 8 && header.AsSpan().SequenceEqual(new byte[] { 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a }))
            return ".png";
        if (read >= 3 && header[0] == 0xff && header[1] == 0xd8 && header[2] == 0xff)
            return ".jpg";
        if (read >= 6 && (header.AsSpan(0, 6).SequenceEqual("GIF87a"u8) || header.AsSpan(0, 6).SequenceEqual("GIF89a"u8)))
            return ".gif";
        return null;
    }

    private void DeleteOtherSlotFormats(CharacterImageSlot slot, string? keepPath)
    {
        foreach (var stem in FileStems(slot))
        foreach (var extension in AllowedExtensions)
        {
            var path = Path.Combine(paths.UserCharacterImages, stem + extension);
            if (!string.Equals(path, keepPath, StringComparison.OrdinalIgnoreCase) && File.Exists(path))
                File.Delete(path);
        }
    }

    private static string[] FileStems(CharacterImageSlot slot) => slot switch
    {
        CharacterImageSlot.Default => ["default"],
        CharacterImageSlot.Back => ["back"],
        CharacterImageSlot.Walk1 => ["walk-1", "walk-left"],
        CharacterImageSlot.Walk2 => ["walk-2", "walk-right"],
        CharacterImageSlot.Walk3 => ["walk-3", "walk-middle"],
        CharacterImageSlot.Eat1 => ["eat-1"],
        CharacterImageSlot.Eat2 => ["eat-2"],
        CharacterImageSlot.Sleep1 => ["sleep-1"],
        CharacterImageSlot.Sleep2 => ["sleep-2"],
        CharacterImageSlot.DragPropeller => ["drag-propeller"],
        CharacterImageSlot.Fall => ["fall"],
        CharacterImageSlot.LandStunned => ["land-stunned"],
        _ => throw new ArgumentOutOfRangeException(nameof(slot), slot, null)
    };
}
