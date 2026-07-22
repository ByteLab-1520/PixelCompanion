using System.Collections.Concurrent;
using System.Text.Json;

namespace PixelCompanion.Core.Services;

public sealed class AtomicJsonStore
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Locks = new(StringComparer.OrdinalIgnoreCase);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public async Task<T> LoadOrCreateAsync<T>(string path, Func<T> defaults, CancellationToken cancellationToken = default)
    {
        var gate = Locks.GetOrAdd(Path.GetFullPath(path), _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            await using var fileLock = await AcquireFileLockAsync(path, cancellationToken);
            if (!File.Exists(path))
            {
                var value = defaults();
                await SaveUnsafeAsync(path, value, cancellationToken);
                return value;
            }

            try
            {
                await using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                return await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, cancellationToken) ?? defaults();
            }
            catch (Exception ex) when (ex is JsonException or IOException)
            {
                var backup = path + ".bak";
                if (File.Exists(backup))
                {
                    await using var stream = File.Open(backup, FileMode.Open, FileAccess.Read, FileShare.Read);
                    var restored = await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, cancellationToken);
                    if (restored is not null)
                    {
                        File.Copy(backup, path, true);
                        return restored;
                    }
                }

                var value = defaults();
                await SaveUnsafeAsync(path, value, cancellationToken);
                return value;
            }
        }
        finally { gate.Release(); }
    }

    public async Task SaveAsync<T>(string path, T value, CancellationToken cancellationToken = default)
    {
        var gate = Locks.GetOrAdd(Path.GetFullPath(path), _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            await using var fileLock = await AcquireFileLockAsync(path, cancellationToken);
            await SaveUnsafeAsync(path, value, cancellationToken);
        }
        finally { gate.Release(); }
    }

    private static async Task<FileStream> AcquireFileLockAsync(string path, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
        var lockPath = path + ".lock";
        for (var attempt = 0; attempt < 100; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                return new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, 1, FileOptions.Asynchronous);
            }
            catch (IOException) when (attempt < 99)
            {
                await Task.Delay(50, cancellationToken);
            }
        }
        throw new IOException($"Could not acquire settings lock for '{path}'.");
    }

    private static async Task SaveUnsafeAsync<T>(string path, T value, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(path) ?? ".";
        Directory.CreateDirectory(directory);
        var temp = path + ".tmp";
        var backup = path + ".bak";

        await using (var stream = File.Open(temp, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await JsonSerializer.SerializeAsync(stream, value, JsonOptions, cancellationToken);
            await stream.FlushAsync(cancellationToken);
        }

        await using (var verify = File.OpenRead(temp))
            _ = await JsonSerializer.DeserializeAsync<T>(verify, JsonOptions, cancellationToken)
                ?? throw new JsonException("Serialized value could not be verified.");

        if (File.Exists(path))
            File.Copy(path, backup, true);
        File.Move(temp, path, true);
    }
}
