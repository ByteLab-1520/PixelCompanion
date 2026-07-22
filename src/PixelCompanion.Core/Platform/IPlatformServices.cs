namespace PixelCompanion.Core.Platform;

public sealed record MediaSnapshot(bool IsAvailable, bool IsPlaying, bool IsVideo, string? Title, string? Artist, string? SourceApplication);
public sealed record BatterySnapshot(bool IsAvailable, bool IsInternalBattery, double? Percent, bool IsPowerConnected, bool IsCharging, bool IsEnergySaver);
public sealed record SystemSnapshot(TimeSpan IdleTime, bool IsFullScreen, bool IsHighLoad);

public interface IPlatformServices
{
    Task<MediaSnapshot> GetMediaAsync(CancellationToken cancellationToken = default);
    Task<BatterySnapshot> GetBatteryAsync(CancellationToken cancellationToken = default);
    Task<SystemSnapshot> GetSystemAsync(CancellationToken cancellationToken = default);
    Task SetAutoStartAsync(bool enabled, CancellationToken cancellationToken = default);
    Task ShowNotificationAsync(string title, string message, CancellationToken cancellationToken = default);
}

public sealed class SafeFallbackPlatformServices : IPlatformServices
{
    public Task<MediaSnapshot> GetMediaAsync(CancellationToken cancellationToken = default) => Task.FromResult(new MediaSnapshot(false, false, false, null, null, null));
    public Task<BatterySnapshot> GetBatteryAsync(CancellationToken cancellationToken = default) => Task.FromResult(new BatterySnapshot(false, false, null, false, false, false));
    public Task<SystemSnapshot> GetSystemAsync(CancellationToken cancellationToken = default) => Task.FromResult(new SystemSnapshot(TimeSpan.Zero, false, false));
    public Task SetAutoStartAsync(bool enabled, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task ShowNotificationAsync(string title, string message, CancellationToken cancellationToken = default) => Task.CompletedTask;
}
