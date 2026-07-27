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
    private readonly IDesktopIntegration _desktopIntegration;
    private readonly DispatcherTimer _movementTimer;
    private readonly DispatcherTimer _animationTimer;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly Image _character;
    private readonly Dictionary<CharacterImageSlot, Bitmap> _bundledCharacters = [];
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
    private MovementRegionCollection _regionCollection = new();
    private IReadOnlyList<MovementSurface> _windowSurfaces = [];
    private string? _currentSurfaceId;
    private double _surfaceRelativeX;
    private double _targetRelativeX;
    private long _nextDesktopPollMs;
    private long _nextSettingsPollMs;
    private DateTime _lastSettingsWriteUtc;
    private DateTime _lastRegionsWriteUtc;
    private bool _loadingSettings;
    private bool _clickThroughApplied;
    private bool? _autoStartApplied;
    private bool _hiddenForFullScreen;
    private bool _transitioning;
    private bool _recovering;
    private bool _fullScreenWasActive;
    private bool _waitingAtFullScreenEdge;
    private string? _surfaceBeforeFullScreen;
    private double _relativeBeforeFullScreen;
    private string _screenSignature = "";
    private IReadOnlyList<CharacterImageSlot> _specialAnimation = [];
    private long _specialAnimationUntilMs;
    private int _specialAnimationFrame;

    public bool BehaviorPaused => _settings.BehaviorPaused;
    public bool ClickThrough => _settings.ClickThrough;
    public bool DoNotDisturb => _settings.DoNotDisturb;
    public bool AutoStart => _settings.AutoStart;
    public MovementSurfaceMode MovementSurfaceMode => _settings.MovementSurfaceMode;
    public event EventHandler? QuickSettingsChanged;

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
        _desktopIntegration = OperatingSystem.IsWindows()
            ? new WindowsDesktopIntegration()
            : new SafeDesktopIntegration();

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

        foreach (var slot in CharacterImageSlots.All)
        {
            var assetName = BundledAssetName(slot);
            _bundledCharacters[slot] = new Bitmap(
                AssetLoader.Open(new Uri($"avares://PixelCompanion/Assets/{assetName}")));
        }
        _character = new Image
        {
            Source = _bundledCharacters[CharacterImageSlot.Default],
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
            await LoadRegionsAsync();
            ApplySettings();
            RefreshDesktopState();
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
        var surfaces = new MenuItem { Header = _localization.Get("menu.movementSurface") };
        surfaces.Items.Add(Item("surface.desktopOnly", () => SetMovementSurfaceModeAsync(MovementSurfaceMode.DesktopOnly)));
        surfaces.Items.Add(Item("surface.windowsOnly", () => SetMovementSurfaceModeAsync(MovementSurfaceMode.WindowsOnly)));
        surfaces.Items.Add(Item("surface.desktopAndWindows", () => SetMovementSurfaceModeAsync(MovementSurfaceMode.DesktopAndWindows)));

        menu.Items.Add(Item("menu.interact", () => ShowRandomGreeting()));
        menu.Items.Add(Item("menu.feed", FeedAsync));
        menu.Items.Add(Item("menu.play", PlayAsync));
        menu.Items.Add(Item("menu.sleep", SleepAsync));
        menu.Items.Add(new Separator());
        menu.Items.Add(pause);
        menu.Items.Add(speed);
        menu.Items.Add(surfaces);
        menu.Items.Add(Item("menu.movementRegions", OpenMovementRegionEditor));
        menu.Items.Add(Item(_settings.ClickThrough ? "menu.disableClickThrough" : "menu.enableClickThrough", ToggleClickThrough));
        menu.Items.Add(Item(_settings.DoNotDisturb ? "menu.disableDnd" : "menu.enableDnd", ToggleDoNotDisturb));
        menu.Items.Add(Item(_settings.AutoStart ? "menu.disableAutoStart" : "menu.enableAutoStart", ToggleAutoStart));
        menu.Items.Add(language);
        menu.Items.Add(Item("menu.advancedSettings", OpenAdvancedSettings));
        menu.Items.Add(_availableUpdate switch
        {
            null => Item("menu.checkUpdates", ManualCheckForUpdates),
            { SupportsAutomaticInstall: true } release =>
                RawItem(string.Format(_localization.Get("menu.installUpdate"), release.TagName), StartUpdate),
            { } release =>
                RawItem(string.Format(_localization.Get("menu.viewUpdate"), release.TagName), OpenUpdatePage)
        });
        menu.Items.Add(Item("menu.hide", HideCharacter));
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
        _specialAnimationUntilMs = 0;
        _specialAnimation = [];
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
        RefreshDesktopState();
        var currentPetWidth = PetPixelWidth();
        var currentPetHeight = PetPixelHeight();
        var petBottom = Position.Y + currentPetHeight;
        var candidates = GetAvailableSurfaces()
            .Where(surface =>
            {
                var ground = surface.Kind == MovementSurfaceKind.WindowTop
                    ? surface.Bounds.Y
                    : surface.Bounds.Bottom;
                return ground >= petBottom - 48;
            })
            .ToArray();
        var surface = MovementGeometry.FindNearest(
            candidates,
            new DesktopPoint(Position.X + (currentPetWidth / 2), petBottom),
            currentPetWidth);
        if (surface is not null &&
            MovementGeometry.TryPlace(
                surface,
                Position.X,
                PetPixelWidth(surface),
                PetPixelHeight(surface),
                out var placement))
        {
            var x = (int)Math.Round(placement.X);
            var floor = (int)Math.Round(placement.Y);
            for (var y = Position.Y; y < floor; y = Math.Min(floor, y + 18))
            {
                Position = new PixelPoint(x, y);
                await Task.Delay(16);
            }
            Position = new PixelPoint(x, floor);
            AttachToSurface(surface, x);
        }
        _character.RenderTransform = null;
    }

    private void UpdateMovement()
    {
        var now = _clock.ElapsedMilliseconds;
        var elapsed = Math.Clamp((now - _lastMovementMs) / 1000d, 0, 0.1);
        _lastMovementMs = now;
        if (_desktopIntegration.IsClickThroughHotKeyPressed())
            ToggleClickThrough();

        if (now >= _nextDesktopPollMs)
        {
            _nextDesktopPollMs = now + (_walking ? 500 : 1000);
            RefreshDesktopState();
        }
        if (now >= _nextSettingsPollMs)
        {
            _nextSettingsPollMs = now + 1000;
            _ = ReloadExternalSettingsAsync();
        }

        var inactive = _dragging || _transitioning || _recovering || now < _specialAnimationUntilMs ||
                       _settings.BehaviorPaused || _settings.DoNotDisturb || !IsVisible;
        SetMovementTimerInterval(inactive ? 500 : _walking ? 66 : 250);
        if (inactive) return;

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
        if (FindSurface(_currentSurfaceId) is { } current)
            _surfaceRelativeX = MovementGeometry.RelativeX(current, Position.X, PetPixelWidth(current));
        _character.RenderTransform = new ScaleTransform(dx < 0 ? -1 : 1, 1);
    }

    private void UpdateAnimation()
    {
        if (_dragging || !IsVisible) return;
        if (++_profilePollTicks >= 4)
        {
            _profilePollTicks = 0;
            var updatedAt = File.Exists(_paths.UserCharacterProfileFile)
                ? File.GetLastWriteTimeUtc(_paths.UserCharacterProfileFile)
                : DateTime.MinValue;
            if (updatedAt != _lastProfileWriteUtc)
                _ = LoadCharacterImagesAsync();
        }

        var now = _clock.ElapsedMilliseconds;
        if (now < _specialAnimationUntilMs && _specialAnimation.Count > 0)
        {
            _animationTimer.Interval = TimeSpan.FromMilliseconds(320);
            _character.Margin = default;
            SetCharacterFrame(_specialAnimation[_specialAnimationFrame++ % _specialAnimation.Count]);
            return;
        }
        if (_specialAnimation.Count > 0)
        {
            _specialAnimation = [];
            _specialAnimationUntilMs = 0;
            SetCharacterFrame(CharacterImageSlot.Default);
        }

        _animationTimer.Interval = TimeSpan.FromMilliseconds(_walking ? 260 : 800);
        _bob = !_bob;
        var margin = new Thickness(0, 0, 0, _walking && _bob ? 4 : 0);
        if (_character.Margin != margin) _character.Margin = margin;
        if (!_walking)
        {
            SetCharacterFrame(CharacterImageSlot.Default);
            return;
        }

        var sequence = new[]
        {
            CharacterImageSlot.Walk1,
            CharacterImageSlot.Walk2,
            CharacterImageSlot.Walk3,
            CharacterImageSlot.Walk2
        };
        SetCharacterFrame(sequence[_animationFrame++ % sequence.Length]);
    }

    private void SetMovementTimerInterval(int milliseconds)
    {
        var interval = TimeSpan.FromMilliseconds(milliseconds);
        if (_movementTimer.Interval != interval)
            _movementTimer.Interval = interval;
    }

    private async Task LoadCharacterImagesAsync()
    {
        if (_loadingCharacterImages) return;
        _loadingCharacterImages = true;
        try
        {
            var profile = await _characterService.LoadAsync();
            var replacements = new Dictionary<CharacterImageSlot, Bitmap>();
            foreach (var slot in CharacterImageSlots.All)
            {
                var path = _characterService.ResolvePath(profile, slot);
                if (path is null) continue;
                try { replacements[slot] = new Bitmap(path); }
                catch (Exception ex) when (ex is IOException or ArgumentException) { }
            }

            _character.Source = _bundledCharacters[CharacterImageSlot.Default];
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
            _characterBitmaps.TryGetValue(CharacterImageSlot.Default, out selected) ||
            _bundledCharacters.TryGetValue(slot, out selected) ||
            _bundledCharacters.TryGetValue(CharacterImageSlot.Default, out selected))
        {
            if (!ReferenceEquals(_character.Source, selected)) _character.Source = selected;
        }
    }

    private static string BundledAssetName(CharacterImageSlot slot) => slot switch
    {
        CharacterImageSlot.Default => "default-cat.png",
        CharacterImageSlot.Back => "default-cat-back.png",
        CharacterImageSlot.Walk1 => "default-cat-walk-1.png",
        CharacterImageSlot.Walk2 => "default-cat-walk-2.png",
        CharacterImageSlot.Walk3 => "default-cat-walk-3.png",
        CharacterImageSlot.Eat1 => "default-cat-eat-1.png",
        CharacterImageSlot.Eat2 => "default-cat-eat-2.png",
        CharacterImageSlot.Sleep1 => "default-cat-sleep-1.png",
        CharacterImageSlot.Sleep2 => "default-cat-sleep-2.png",
        _ => "default-cat.png"
    };

    private void StartSpecialAnimation(IReadOnlyList<CharacterImageSlot> frames, int durationMilliseconds)
    {
        _walking = false;
        _specialAnimation = frames;
        _specialAnimationFrame = 0;
        _specialAnimationUntilMs = _clock.ElapsedMilliseconds + durationMilliseconds;
        SetCharacterFrame(frames[0]);
    }

    private void ChooseTarget()
    {
        var available = GetAvailableSurfaces().Where(surface => surface.IsValidFor(PetPixelWidth(surface))).ToArray();
        if (available.Length == 0) return;

        var current = FindSurface(_currentSurfaceId);
        var surface = current is not null && available.Any(candidate => candidate.Id == current.Id) && Random.Shared.NextDouble() < 0.78
            ? current
            : available[Random.Shared.Next(available.Length)];
        if (current is not null && surface.Id != current.Id)
        {
            _ = TransitionToSurfaceAsync(surface);
            return;
        }
        var surfacePetWidth = PetPixelWidth(surface);
        var travel = Math.Max(0, surface.Bounds.Width - surfacePetWidth);
        _targetRelativeX = Random.Shared.NextDouble();
        var requestedX = surface.Bounds.X + (travel * _targetRelativeX);
        if (!MovementGeometry.TryPlace(
                surface,
                requestedX,
                surfacePetWidth,
                PetPixelHeight(surface),
                out var placement)) return;
        _currentSurfaceId = surface.Id;
        _target = new PixelPoint((int)Math.Round(placement.X), (int)Math.Round(placement.Y));
    }

    private async Task TransitionToSurfaceAsync(MovementSurface surface)
    {
        if (_transitioning) return;
        _transitioning = true;
        _walking = false;
        try
        {
            Hide();
            await Task.Delay(220);
            var petWidth = PetPixelWidth(surface);
            var enterFromLeft = Random.Shared.Next(2) == 0;
            var edgeX = enterFromLeft ? surface.Bounds.X : surface.Bounds.Right - petWidth;
            if (!MovementGeometry.TryPlace(
                    surface,
                    edgeX,
                    petWidth,
                    PetPixelHeight(surface),
                    out var placement)) return;
            Position = new PixelPoint((int)Math.Round(placement.X), (int)Math.Round(placement.Y));
            AttachToSurface(surface, placement.X);
            if (_settings.CharacterVisible && !_hiddenForFullScreen) Show();
            _nextDecisionMs = _clock.ElapsedMilliseconds + 650;
        }
        finally
        {
            _transitioning = false;
        }
    }

    private void PlaceInitially()
    {
        var screen = Screens.Primary;
        if (screen is null) return;
        var surface = CreateDesktopSurface(screen);
        var petWidth = PetPixelWidth(surface);
        var petHeight = PetPixelHeight(surface);
        Position = new PixelPoint(
            screen.WorkingArea.Right - (int)Math.Round(petWidth) - 32,
            screen.WorkingArea.Bottom - (int)Math.Round(petHeight));
        AttachToSurface(surface, Position.X);
    }

    private void RefreshDesktopState()
    {
        _windowSurfaces = _desktopIntegration.GetWindowSurfaces(_settings.ExcludedWindowProcesses);
        HandleFullScreen();

        var signature = string.Join("|", Screens.All.Select(screen =>
            $"{screen.Bounds.X},{screen.Bounds.Y},{screen.Bounds.Width},{screen.Bounds.Height}:" +
            $"{screen.WorkingArea.X},{screen.WorkingArea.Y},{screen.WorkingArea.Width},{screen.WorkingArea.Height}:" +
            $"{screen.Scaling:0.###}"));
        var screensChanged = signature != _screenSignature;
        _screenSignature = signature;

        var current = FindSurface(_currentSurfaceId);
        if (current is null)
        {
            RecoverToNearestSurface();
            return;
        }

        var requestedX = _walking
            ? MovementGeometry.ResolveRelativeX(current, _targetRelativeX, PetPixelWidth(current))
            : MovementGeometry.ResolveRelativeX(current, _surfaceRelativeX, PetPixelWidth(current));
        if (!MovementGeometry.TryPlace(
                current,
                requestedX,
                PetPixelWidth(current),
                PetPixelHeight(current),
                out var placement))
        {
            RecoverToNearestSurface();
            return;
        }

        if (_walking)
        {
            _target = new PixelPoint((int)Math.Round(placement.X), (int)Math.Round(placement.Y));
        }
        else if (current.Kind == MovementSurfaceKind.WindowTop || screensChanged)
        {
            Position = new PixelPoint((int)Math.Round(placement.X), (int)Math.Round(placement.Y));
        }
    }

    private void HandleFullScreen()
    {
        var fullScreen = _desktopIntegration.IsForegroundFullScreen();
        if (fullScreen && !_fullScreenWasActive)
        {
            _fullScreenWasActive = true;
            _surfaceBeforeFullScreen = _currentSurfaceId;
            _relativeBeforeFullScreen = _surfaceRelativeX;
        }
        else if (!fullScreen && _fullScreenWasActive)
        {
            _fullScreenWasActive = false;
            if (_waitingAtFullScreenEdge && FindSurface(_surfaceBeforeFullScreen) is { } previous &&
                MovementGeometry.TryPlace(
                    previous,
                    MovementGeometry.ResolveRelativeX(previous, _relativeBeforeFullScreen, PetPixelWidth(previous)),
                    PetPixelWidth(previous),
                    PetPixelHeight(previous),
                    out var restored))
            {
                Position = new PixelPoint((int)Math.Round(restored.X), (int)Math.Round(restored.Y));
                AttachToSurface(previous, restored.X);
            }
            _waitingAtFullScreenEdge = false;
            _surfaceBeforeFullScreen = null;
        }

        if (_settings.FullScreenBehavior == FullScreenBehavior.Hide)
        {
            if (fullScreen && IsVisible)
            {
                _hiddenForFullScreen = true;
                Hide();
            }
            else if (!fullScreen && _hiddenForFullScreen)
            {
                _hiddenForFullScreen = false;
                if (_settings.CharacterVisible) Show();
            }
            return;
        }

        if (!fullScreen || _settings.FullScreenBehavior != FullScreenBehavior.WaitAtEdge)
            return;

        _waitingAtFullScreenEdge = true;
        _walking = false;
        _bubble.IsVisible = false;
        var screen = Screens.ScreenFromWindow(this) ?? Screens.Primary;
        if (screen is null) return;
        var area = screen.WorkingArea;
        var surface = CreateDesktopSurface(screen);
        Position = new PixelPoint(
            Math.Max(area.X, area.Right - (int)Math.Round(PetPixelWidth(surface))),
            Math.Max(area.Y, area.Bottom - (int)Math.Round(PetPixelHeight(surface))));
        AttachToSurface(surface, Position.X);
    }

    private IReadOnlyList<MovementSurface> GetAvailableSurfaces()
    {
        var surfaces = new List<MovementSurface>();
        if (_settings.MovementSurfaceMode is MovementSurfaceMode.DesktopOnly or MovementSurfaceMode.DesktopAndWindows)
            surfaces.AddRange(GetDesktopSurfaces());
        if (_settings.MovementSurfaceMode is MovementSurfaceMode.WindowsOnly or MovementSurfaceMode.DesktopAndWindows)
            surfaces.AddRange(_windowSurfaces);

        if (surfaces.Count == 0)
        {
            var fallback = Screens.Primary ?? Screens.ScreenFromWindow(this);
            if (fallback is not null) surfaces.Add(CreateDesktopSurface(fallback));
        }
        return surfaces;
    }

    private IEnumerable<MovementSurface> GetDesktopSurfaces()
    {
        if (_settings.RegionMode == RegionMode.Custom)
        {
            foreach (var region in _regionCollection.Regions.Where(region => region.IsValid))
            {
                yield return new MovementSurface(
                    $"region:{region.Id}",
                    MovementSurfaceKind.CustomRegion,
                    new DesktopRect(region.X, region.Y, region.Width, region.Height));
            }
            yield break;
        }

        IEnumerable<Screen> selected = _settings.RegionMode switch
        {
            RegionMode.AllMonitors => Screens.All,
            RegionMode.CurrentMonitor => new[] { Screens.ScreenFromWindow(this) ?? Screens.Primary }.OfType<Screen>(),
            _ => new[] { Screens.Primary }.OfType<Screen>()
        };
        foreach (var screen in selected)
            yield return CreateDesktopSurface(screen);
    }

    private MovementSurface CreateDesktopSurface(Screen screen)
    {
        var area = _settings.IncludeSystemAreas ? screen.Bounds : screen.WorkingArea;
        return new MovementSurface(
            $"desktop:{screen.Bounds.X}:{screen.Bounds.Y}:{screen.Bounds.Width}:{screen.Bounds.Height}",
            MovementSurfaceKind.DesktopFloor,
            new DesktopRect(area.X, area.Y, area.Width, area.Height));
    }

    private MovementSurface? FindSurface(string? id)
    {
        if (id is null) return null;
        return GetAvailableSurfaces().FirstOrDefault(surface => surface.Id == id);
    }

    private void AttachToSurface(MovementSurface surface, double x)
    {
        _currentSurfaceId = surface.Id;
        _surfaceRelativeX = MovementGeometry.RelativeX(surface, x, PetPixelWidth(surface));
        _targetRelativeX = _surfaceRelativeX;
    }

    private void RecoverToNearestSurface()
    {
        if (_recovering) return;
        var surfaces = GetAvailableSurfaces();
        var currentPetWidth = PetPixelWidth();
        var petBottom = Position.Y + PetPixelHeight();
        var below = surfaces.Where(surface =>
        {
            var ground = surface.Kind == MovementSurfaceKind.WindowTop
                ? surface.Bounds.Y
                : surface.Bounds.Bottom;
            return ground >= petBottom - 48;
        }).ToArray();
        var nearest = MovementGeometry.FindNearest(
            below.Length > 0 ? below : surfaces,
            new DesktopPoint(Position.X + (currentPetWidth / 2), Position.Y + PetPixelHeight()),
            currentPetWidth);
        if (nearest is null) return;
        if (!MovementGeometry.TryPlace(
                nearest,
                Position.X,
                PetPixelWidth(nearest),
                PetPixelHeight(nearest),
                out var placement)) return;
        _ = RecoverToSurfaceAsync(nearest, placement);
    }

    private async Task RecoverToSurfaceAsync(MovementSurface surface, SurfacePlacement placement)
    {
        if (_recovering) return;
        _recovering = true;
        _walking = false;
        try
        {
            SetCharacterFrame(CharacterImageSlot.Default);
            _character.RenderTransform = new RotateTransform(-4);
            var destinationY = (int)Math.Round(placement.Y);
            if (destinationY >= Position.Y)
            {
                var x = (int)Math.Round(placement.X);
                for (var y = Position.Y; y < destinationY; y = Math.Min(destinationY, y + 18))
                {
                    Position = new PixelPoint(x, y);
                    await Task.Delay(16);
                }
            }
            Position = new PixelPoint((int)Math.Round(placement.X), destinationY);
            AttachToSurface(surface, placement.X);
            _nextDecisionMs = _clock.ElapsedMilliseconds + Random.Shared.Next(2500, 6000);
        }
        finally
        {
            _character.RenderTransform = null;
            _recovering = false;
        }
    }

    private async Task LoadRegionsAsync()
    {
        _regionCollection = await _store.LoadOrCreateAsync(_paths.RegionsFile, () => new MovementRegionCollection());
        _lastRegionsWriteUtc = File.Exists(_paths.RegionsFile)
            ? File.GetLastWriteTimeUtc(_paths.RegionsFile)
            : DateTime.MinValue;
    }

    private async Task ReloadExternalSettingsAsync()
    {
        if (_loadingSettings) return;
        _loadingSettings = true;
        try
        {
            var settingsWrite = File.Exists(_paths.SettingsFile)
                ? File.GetLastWriteTimeUtc(_paths.SettingsFile)
                : DateTime.MinValue;
            if (settingsWrite != _lastSettingsWriteUtc)
            {
                _settings = await _store.LoadOrCreateAsync(_paths.SettingsFile, () => new AppSettings());
                _lastSettingsWriteUtc = settingsWrite;
                ApplySettings();
                QuickSettingsChanged?.Invoke(this, EventArgs.Empty);
            }

            var regionsWrite = File.Exists(_paths.RegionsFile)
                ? File.GetLastWriteTimeUtc(_paths.RegionsFile)
                : DateTime.MinValue;
            if (regionsWrite != _lastRegionsWriteUtc)
                await LoadRegionsAsync();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            // AtomicJsonStore will restore a backup on the next successful read.
        }
        finally
        {
            _loadingSettings = false;
        }
    }

    private double PetPixelWidth(MovementSurface? surface = null) => Width * ScalingFor(surface);
    private double PetPixelHeight(MovementSurface? surface = null) => Height * ScalingFor(surface);

    private double ScalingFor(MovementSurface? surface)
    {
        var point = surface is null
            ? new PixelPoint(Position.X + 1, Position.Y + 1)
            : new PixelPoint(
                (int)Math.Round(surface.Bounds.X + (surface.Bounds.Width / 2)),
                (int)Math.Round(surface.Bounds.Y + (surface.Bounds.Height / 2)));
        return Screens.ScreenFromPoint(point)?.Scaling ?? Screens.Primary?.Scaling ?? 1;
    }

    private void ApplySettings()
    {
        Topmost = _settings.AlwaysOnTop;
        Opacity = Math.Clamp(_settings.Opacity, 0.2, 1);
        var scale = Math.Clamp(_settings.CharacterScale, 1, 6);
        Width = Height = Math.Max(128, 105 * scale);
        _character.Width = _character.Height = 88 * scale;
        var handle = TryGetPlatformHandle()?.Handle ?? nint.Zero;
        if (_clickThroughApplied != _settings.ClickThrough || handle != nint.Zero)
        {
            _desktopIntegration.SetClickThrough(handle, _settings.ClickThrough);
            _clickThroughApplied = _settings.ClickThrough;
        }
        if (_autoStartApplied != _settings.AutoStart)
        {
            _desktopIntegration.SetAutoStart(_settings.AutoStart);
            _autoStartApplied = _settings.AutoStart;
        }
        ContextMenu = BuildContextMenu();
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
        StartSpecialAnimation([CharacterImageSlot.Eat1, CharacterImageSlot.Eat2], 3200);
        ShowBubble(_localization.Get("dialogue.feed"));
        await _store.SaveAsync(_paths.PetStateFile, _petState);
    }

    private void SleepAsync()
    {
        StartSpecialAnimation([CharacterImageSlot.Sleep1, CharacterImageSlot.Sleep2], 8000);
        ShowBubble(_localization.Get("dialogue.sleep"));
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
        QuickSettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    public async void TogglePause()
    {
        _settings = _settings with { BehaviorPaused = !_settings.BehaviorPaused };
        ContextMenu = BuildContextMenu();
        await SaveSettingsAsync();
        QuickSettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    public async void ShowCharacter()
    {
        _hiddenForFullScreen = false;
        _settings = _settings with { CharacterVisible = true };
        if (!IsVisible) Show();
        Activate();
        await SaveSettingsAsync();
    }

    public async void HideCharacter()
    {
        _settings = _settings with { CharacterVisible = false };
        Hide();
        await SaveSettingsAsync();
    }

    public async void ToggleClickThrough()
    {
        _settings = _settings with { ClickThrough = !_settings.ClickThrough };
        ApplySettings();
        await SaveSettingsAsync();
        QuickSettingsChanged?.Invoke(this, EventArgs.Empty);
        if (_settings.ClickThrough)
            ShowBubble(_localization.Get("clickThrough.enabledHint"));
    }

    public async void ToggleDoNotDisturb()
    {
        _settings = _settings with { DoNotDisturb = !_settings.DoNotDisturb };
        if (_settings.DoNotDisturb)
        {
            _bubble.IsVisible = false;
            MoveToScreenEdge();
        }
        ContextMenu = BuildContextMenu();
        await SaveSettingsAsync();
        QuickSettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void MoveToScreenEdge()
    {
        var screen = Screens.ScreenFromWindow(this) ?? Screens.Primary;
        if (screen is null) return;
        var surface = CreateDesktopSurface(screen);
        if (!MovementGeometry.TryPlace(
                surface,
                surface.Bounds.Right,
                PetPixelWidth(surface),
                PetPixelHeight(surface),
                out var placement)) return;
        Position = new PixelPoint((int)Math.Round(placement.X), (int)Math.Round(placement.Y));
        AttachToSurface(surface, placement.X);
        _walking = false;
    }

    public async void ToggleAutoStart()
    {
        var requested = !_settings.AutoStart;
        var applied = _desktopIntegration.SetAutoStart(requested);
        _settings = _settings with { AutoStart = applied && requested };
        ContextMenu = BuildContextMenu();
        await SaveSettingsAsync();
        QuickSettingsChanged?.Invoke(this, EventArgs.Empty);
        if (!applied) ShowBubble(_localization.Get("autoStart.failed"));
    }

    public async void SetMovementSurfaceModeAsync(MovementSurfaceMode mode)
    {
        _settings = _settings with { MovementSurfaceMode = mode };
        ContextMenu = BuildContextMenu();
        RefreshDesktopState();
        await SaveSettingsAsync();
        QuickSettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void OpenMovementRegionEditor()
    {
        var editor = new MovementRegionEditorWindow(_paths, _store, _localization);
        editor.Closed += async (_, _) =>
        {
            await LoadRegionsAsync();
            RefreshDesktopState();
        };
        editor.Show();
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
                    ShowBubble(string.Format(
                        _localization.Get(result.Release.SupportsAutomaticInstall
                            ? "update.available"
                            : "update.availableManual"),
                        result.Release.TagName));
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

    private void OpenUpdatePage()
    {
        if (_availableUpdate is null) return;
        try
        {
            Process.Start(new ProcessStartInfo(_availableUpdate.ReleasePage.AbsoluteUri)
            {
                UseShellExecute = true
            });
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            ShowBubble(_localization.Get("update.failed"));
        }
    }

    private async void StartUpdate()
    {
        if (_availableUpdate is not { SupportsAutomaticInstall: true }) return;
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

    private async Task SaveSettingsAsync()
    {
        await _store.SaveAsync(_paths.SettingsFile, _settings);
        _lastSettingsWriteUtc = File.Exists(_paths.SettingsFile)
            ? File.GetLastWriteTimeUtc(_paths.SettingsFile)
            : DateTime.MinValue;
    }
    private Task PersistAsync() => Task.WhenAll(SaveSettingsAsync(), _store.SaveAsync(_paths.PetStateFile, _petState));
}
