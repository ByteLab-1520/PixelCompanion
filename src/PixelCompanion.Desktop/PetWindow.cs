using System.Diagnostics;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using PixelCompanion.Core.Models;
using PixelCompanion.Core.Services;

namespace PixelCompanion.Desktop;

public sealed class PetWindow : Window
{
    private readonly IClassicDesktopStyleApplicationLifetime _lifetime;
    private readonly AppPaths _paths;
    private readonly AtomicJsonStore _store;
    private readonly LocalizationService _localization;
    private readonly PetStateService _petStateService = new();
    private readonly DialogueSelector _dialogues = new();
    private readonly UserCharacterService _characterService;
    private readonly DispatcherTimer _movementTimer;
    private readonly DispatcherTimer _animationTimer;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly Image _character;
    private readonly Bitmap _bundledCharacter;
    private readonly Dictionary<CharacterImageSlot, Bitmap> _characterBitmaps = [];
    private readonly Border _bubble;
    private readonly TextBlock _bubbleText;
    private AppSettings _settings;
    private PetState _petState;
    private PixelPoint _dragPointerStart;
    private PixelPoint _dragWindowStart;
    private bool _dragging;
    private bool _walking;
    private PixelPoint _target;
    private long _lastMovementMs;
    private long _nextDecisionMs;
    private bool _bob;
    private int _animationFrame;
    private int _profilePollTicks;
    private DateTime _lastProfileWriteUtc;
    private bool _loadingCharacterImages;
    private ReleaseUpdateInfo? _availableUpdate;
    private bool _checkingForUpdates;

    public bool BehaviorPaused => _settings.BehaviorPaused;

    public PetWindow(IClassicDesktopStyleApplicationLifetime lifetime, AppPaths paths, AtomicJsonStore store,
        LocalizationService localization, AppSettings settings, PetState petState)
    {
        _lifetime = lifetime;
        _paths = paths;
        _store = store;
        _localization = localization;
        _settings = settings;
        _petState = petState;
        _characterService = new UserCharacterService(paths, store);

        Width = Height = 210;
        MinWidth = MinHeight = 128;
        WindowDecorations = Avalonia.Controls.WindowDecorations.None;
        TransparencyLevelHint = [WindowTransparencyLevel.Transparent];
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        CanResize = false;
        Topmost = settings.AlwaysOnTop;
        Opacity = settings.Opacity;
        Title = localization.Get("app.name");

        _bundledCharacter = new Bitmap(AssetLoader.Open(new Uri("avares://PixelCompanion/Assets/default-cat.png")));
        _character = new Image
        {
            Source = _bundledCharacter,
            Width = 176,
            Height = 176,
            Stretch = Stretch.Uniform,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Bottom,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
        };
        RenderOptions.SetBitmapInterpolationMode(_character, BitmapInterpolationMode.None);

        _bubbleText = new TextBlock { TextWrapping = TextWrapping.Wrap, MaxWidth = 190, FontSize = 13 };
        _bubble = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(238, 255, 255, 255)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(65, 82, 82)),
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(10, 7),
            Child = _bubbleText,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top,
            IsVisible = false
        };

        var canvas = new Grid();
        canvas.Children.Add(_character);
        canvas.Children.Add(_bubble);
        Content = canvas;
        ContextMenu = BuildContextMenu();

        PointerPressed += OnPointerPressed;
        PointerMoved += OnPointerMoved;
        PointerReleased += OnPointerReleased;
        Opened += async (_, _) =>
        {
            PlaceInitially();
            await LoadCharacterImagesAsync();
            await CheckForUpdatesAsync(showResult: false);
        };
        Closing += async (_, _) => await PersistAsync();

        _movementTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
        _movementTimer.Tick += (_, _) => UpdateMovement();
        _movementTimer.Start();
        _animationTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(260) };
        _animationTimer.Tick += (_, _) => UpdateAnimation();
        _animationTimer.Start();
        _nextDecisionMs = 2500;
    }

    private ContextMenu BuildContextMenu()
    {
        var menu = new ContextMenu();
        var pause = Item(_settings.BehaviorPaused ? "menu.resume" : "menu.pause", async () =>
        {
            _settings = _settings with { BehaviorPaused = !_settings.BehaviorPaused };
            await SaveSettingsAsync();
            ContextMenu = BuildContextMenu();
        });
        var speed = new MenuItem { Header = _localization.Get("menu.speed") };
        speed.Items.Add(Item("speed.verySlow", () => SetSpeedAsync(MovementSpeed.VerySlow)));
        speed.Items.Add(Item("speed.slow", () => SetSpeedAsync(MovementSpeed.Slow)));
        speed.Items.Add(Item("speed.normal", () => SetSpeedAsync(MovementSpeed.Normal)));
        speed.Items.Add(Item("speed.fast", () => SetSpeedAsync(MovementSpeed.Fast)));
        var language = new MenuItem { Header = _localization.Get("menu.language") };
        language.Items.Add(RawItem("English", () => ChangeLanguageAsync("en")));
        language.Items.Add(RawItem("한국어", () => ChangeLanguageAsync("ko")));

        menu.Items.Add(Item("menu.interact", () => ShowRandomGreeting()));
        menu.Items.Add(Item("menu.feed", FeedAsync));
        menu.Items.Add(Item("menu.play", PlayAsync));
        menu.Items.Add(Item("menu.sleep", () => ShowBubble(_localization.Get("dialogue.sleep"))));
        menu.Items.Add(new Separator());
        menu.Items.Add(pause);
        menu.Items.Add(speed);
        menu.Items.Add(language);
        menu.Items.Add(Item("menu.advancedSettings", OpenAdvancedSettings));
        menu.Items.Add(_availableUpdate is null
            ? Item("menu.checkUpdates", ManualCheckForUpdates)
            : RawItem(string.Format(_localization.Get("menu.installUpdate"), _availableUpdate.TagName), StartUpdate));
        menu.Items.Add(Item("menu.hide", Hide));
        menu.Items.Add(new Separator());
        menu.Items.Add(Item("menu.exit", () => _lifetime.Shutdown()));
        return menu;
    }

    private MenuItem Item(string key, Action action) => RawItem(_localization.Get(key), action);
    private static MenuItem RawItem(string text, Action action)
    {
        var item = new MenuItem { Header = text };
        item.Click += (_, _) => action();
        return item;
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var point = e.GetCurrentPoint(this);
        if (point.Properties.IsRightButtonPressed) return;
        if (!point.Properties.IsLeftButtonPressed) return;
        _dragging = true;
        _walking = false;
        _dragPointerStart = this.PointToScreen(point.Position);
        _dragWindowStart = Position;
        e.Pointer.Capture(this);
        SetCharacterFrame(CharacterImageSlot.Default);
        _character.RenderTransform = new RotateTransform(6);
        e.Handled = true;
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_dragging) return;
        var current = this.PointToScreen(e.GetPosition(this));
        Position = new PixelPoint(_dragWindowStart.X + current.X - _dragPointerStart.X, _dragWindowStart.Y + current.Y - _dragPointerStart.Y);
    }

    private async void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_dragging)
        {
            ShowRandomGreeting();
            return;
        }
        _dragging = false;
        e.Pointer.Capture(null);
        await LandAsync();
    }

    private async Task LandAsync()
    {
        _character.RenderTransform = new RotateTransform(-4);
        var screen = Screens.ScreenFromWindow(this);
        if (screen is not null)
        {
            var area = screen.WorkingArea;
            var x = Math.Clamp(Position.X, area.X, Math.Max(area.X, area.Right - (int)Width));
            var floor = Math.Max(area.Y, area.Bottom - (int)Height);
            for (var y = Position.Y; y < floor; y = Math.Min(floor, y + 18))
            {
                Position = new PixelPoint(x, y);
                await Task.Delay(16);
            }
            Position = new PixelPoint(x, floor);
        }
        _character.RenderTransform = null;
    }

    private void UpdateMovement()
    {
        var now = _clock.ElapsedMilliseconds;
        var elapsed = Math.Clamp((now - _lastMovementMs) / 1000d, 0, 0.1);
        _lastMovementMs = now;
        if (_dragging || _settings.BehaviorPaused || _settings.DoNotDisturb || !IsVisible) return;

        if (!_walking && now >= _nextDecisionMs)
        {
            ChooseTarget();
            _walking = true;
        }
        if (!_walking) return;

        var dx = _target.X - Position.X;
        var dy = _target.Y - Position.Y;
        var distance = Math.Sqrt(dx * dx + dy * dy);
        if (distance < 4)
        {
            _walking = false;
            _nextDecisionMs = now + Random.Shared.Next(5000, 11000);
            return;
        }

        var step = Math.Min(distance, PixelsPerSecond() * elapsed);
        Position = new PixelPoint((int)(Position.X + dx / distance * step), (int)(Position.Y + dy / distance * step));
        _character.RenderTransform = new ScaleTransform(dx < 0 ? -1 : 1, 1);
    }

    private void UpdateAnimation()
    {
        if (_dragging) return;
        if (++_profilePollTicks >= 4)
        {
            _profilePollTicks = 0;
            var updatedAt = File.Exists(_paths.UserCharacterProfileFile)
                ? File.GetLastWriteTimeUtc(_paths.UserCharacterProfileFile)
                : DateTime.MinValue;
            if (updatedAt != _lastProfileWriteUtc)
                _ = LoadCharacterImagesAsync();
        }

        _bob = !_bob;
        _character.Margin = new Thickness(0, 0, 0, _walking && _bob ? 4 : 0);
        if (!_walking)
        {
            SetCharacterFrame(CharacterImageSlot.Default);
            return;
        }

        var sequence = new[]
        {
            CharacterImageSlot.WalkLeft,
            CharacterImageSlot.WalkMiddle,
            CharacterImageSlot.WalkRight,
            CharacterImageSlot.WalkMiddle
        };
        SetCharacterFrame(sequence[_animationFrame++ % sequence.Length]);
    }

    private async Task LoadCharacterImagesAsync()
    {
        if (_loadingCharacterImages) return;
        _loadingCharacterImages = true;
        try
        {
            var profile = await _characterService.LoadAsync();
            var replacements = new Dictionary<CharacterImageSlot, Bitmap>();
            foreach (var slot in Enum.GetValues<CharacterImageSlot>())
            {
                var path = _characterService.ResolvePath(profile, slot);
                if (path is null) continue;
                try { replacements[slot] = new Bitmap(path); }
                catch (Exception ex) when (ex is IOException or ArgumentException) { }
            }

            _character.Source = _bundledCharacter;
            foreach (var bitmap in _characterBitmaps.Values) bitmap.Dispose();
            _characterBitmaps.Clear();
            foreach (var pair in replacements) _characterBitmaps[pair.Key] = pair.Value;
            _lastProfileWriteUtc = File.Exists(_paths.UserCharacterProfileFile)
                ? File.GetLastWriteTimeUtc(_paths.UserCharacterProfileFile)
                : DateTime.MinValue;
            SetCharacterFrame(CharacterImageSlot.Default);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            // Keep the currently loaded images if the editor is saving at this exact moment.
        }
        finally
        {
            _loadingCharacterImages = false;
        }
    }

    private void SetCharacterFrame(CharacterImageSlot slot)
    {
        if (_characterBitmaps.TryGetValue(slot, out var selected) ||
            _characterBitmaps.TryGetValue(CharacterImageSlot.Default, out selected))
        {
            _character.Source = selected;
            return;
        }

        _character.Source = _bundledCharacter;
    }

    private void ChooseTarget()
    {
        var screen = Screens.ScreenFromWindow(this) ?? Screens.Primary;
        if (screen is null) return;
        var area = screen.WorkingArea;
        var maxX = Math.Max(area.X, area.Right - (int)Width);
        var floor = Math.Max(area.Y, area.Bottom - (int)Height);
        _target = new PixelPoint(Random.Shared.Next(area.X, maxX + 1), floor);
    }

    private void PlaceInitially()
    {
        var screen = Screens.Primary;
        if (screen is null) return;
        Position = new PixelPoint(screen.WorkingArea.Right - (int)Width - 32, screen.WorkingArea.Bottom - (int)Height);
    }

    private double PixelsPerSecond() => _settings.MovementSpeed switch
    {
        MovementSpeed.VerySlow => 18,
        MovementSpeed.Slow => 30,
        MovementSpeed.Normal => 48,
        MovementSpeed.Fast => 75,
        _ => Math.Clamp(_settings.CustomPixelsPerSecond, 5, 240)
    };

    private void ShowRandomGreeting()
    {
        var lines = new[]
        {
            new DialogueLine("click.1", _localization.Get("dialogue.click.1")),
            new DialogueLine("click.2", _localization.Get("dialogue.click.2"))
        };
        var selected = _dialogues.Select(lines, _petState.Affection, DateTimeOffset.UtcNow);
        if (selected is not null) ShowBubble(selected.Text);
    }

    private async void ShowBubble(string text)
    {
        _bubbleText.Text = text;
        _bubble.IsVisible = true;
        await Task.Delay(3000);
        _bubble.IsVisible = false;
    }

    private async void FeedAsync()
    {
        _petState = _petStateService.Feed(_petState, DateTimeOffset.UtcNow);
        ShowBubble(_localization.Get("dialogue.feed"));
        await _store.SaveAsync(_paths.PetStateFile, _petState);
    }

    private async void PlayAsync()
    {
        _petState = _petStateService.Play(_petState, DateTimeOffset.UtcNow);
        ShowBubble(_localization.Get("dialogue.play"));
        await _store.SaveAsync(_paths.PetStateFile, _petState);
    }

    private async void SetSpeedAsync(MovementSpeed speed)
    {
        _settings = _settings with { MovementSpeed = speed };
        await SaveSettingsAsync();
    }

    private async void ChangeLanguageAsync(string language)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "locales", language + ".json");
        var resources = await LocalizationService.LoadFileAsync(path);
        _localization.ChangeLanguage(language, resources);
        _settings = _settings with { Language = language };
        Title = _localization.Get("app.name");
        ContextMenu = BuildContextMenu();
        await SaveSettingsAsync();
    }

    public async void TogglePause()
    {
        _settings = _settings with { BehaviorPaused = !_settings.BehaviorPaused };
        ContextMenu = BuildContextMenu();
        await SaveSettingsAsync();
    }

    public void OpenAdvancedSettings()
    {
        var name = OperatingSystem.IsWindows() ? "PixelCompanion.Config.exe" : "PixelCompanion.Config";
        var path = Path.Combine(AppContext.BaseDirectory, name);
        if (File.Exists(path)) Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }

    private async Task CheckForUpdatesAsync(bool showResult)
    {
        if (_checkingForUpdates) return;
        if (!showResult && !_settings.AutoCheckUpdates) return;
        _checkingForUpdates = true;
        try
        {
            var updateState = await _store.LoadOrCreateAsync(_paths.UpdateStateFile, () => new UpdateState());
            if (!showResult && updateState.LastCheckedAtUtc is { } lastChecked &&
                DateTimeOffset.UtcNow - lastChecked < TimeSpan.FromDays(1))
                return;

            var currentVersion = Assembly.GetEntryAssembly()?.GetName().Version ?? new Version(0, 1, 0);
            var result = await new GitHubReleaseUpdateService().CheckAsync(currentVersion);
            await _store.SaveAsync(_paths.UpdateStateFile, updateState with
            {
                LastCheckedAtUtc = DateTimeOffset.UtcNow,
                LatestTag = result.Release?.TagName
            });

            if (result.IsUpdateAvailable && result.Release is not null)
            {
                _availableUpdate = result.Release;
                ContextMenu = BuildContextMenu();
                if (showResult)
                    ShowBubble(string.Format(_localization.Get("update.available"), result.Release.TagName));
            }
            else if (showResult)
            {
                ShowBubble(result.Error is null
                    ? _localization.Get("update.current")
                    : _localization.Get("update.failed"));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            if (showResult) ShowBubble(_localization.Get("update.failed"));
        }
        finally
        {
            _checkingForUpdates = false;
        }
    }

    private async void ManualCheckForUpdates() => await CheckForUpdatesAsync(showResult: true);

    private async void StartUpdate()
    {
        if (_availableUpdate is null) return;
        var installedUpdater = Path.Combine(AppContext.BaseDirectory, "PixelCompanion.Updater.exe");
        if (!File.Exists(installedUpdater))
        {
            ShowBubble(_localization.Get("update.updaterMissing"));
            return;
        }

        try
        {
            var runnerDirectory = Path.Combine(_paths.Updates, "runner");
            Directory.CreateDirectory(runnerDirectory);
            var runner = Path.Combine(runnerDirectory, "PixelCompanion.Updater.exe");
            File.Copy(installedUpdater, runner, true);
            var currentVersion = Assembly.GetEntryAssembly()?.GetName().Version ?? new Version(0, 1, 0);
            var startInfo = new ProcessStartInfo(runner) { UseShellExecute = true };
            startInfo.ArgumentList.Add("--current-pid");
            startInfo.ArgumentList.Add(Environment.ProcessId.ToString());
            startInfo.ArgumentList.Add("--install-dir");
            startInfo.ArgumentList.Add(AppContext.BaseDirectory);
            startInfo.ArgumentList.Add("--current-version");
            startInfo.ArgumentList.Add(currentVersion.ToString(3));
            startInfo.ArgumentList.Add("--data-dir");
            startInfo.ArgumentList.Add(_paths.Root);
            Process.Start(startInfo);
            await PersistAsync();
            _lifetime.Shutdown();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            ShowBubble(_localization.Get("update.failed"));
        }
    }

    private Task SaveSettingsAsync() => _store.SaveAsync(_paths.SettingsFile, _settings);
    private Task PersistAsync() => Task.WhenAll(SaveSettingsAsync(), _store.SaveAsync(_paths.PetStateFile, _petState));
}
