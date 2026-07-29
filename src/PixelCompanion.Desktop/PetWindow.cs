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
    private readonly AssistantTimerService _assistantTimerService = new();
    private readonly DialogueSelector _dialogues = new();
    private readonly UserCharacterService _characterService;
    private readonly CharacterDialogueService _dialogueService;
    private readonly IDesktopIntegration _desktopIntegration;
    private readonly DispatcherTimer _movementTimer;
    private readonly DispatcherTimer _animationTimer;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly Image _character;
    private readonly Border _landingIndicator;
    private readonly Dictionary<CharacterImageSlot, Bitmap> _bundledCharacters = [];
    private readonly Dictionary<CharacterImageSlot, Bitmap> _characterBitmaps = [];
    private readonly Border _bubble;
    private readonly TextBlock _bubbleText;
    private CharacterDialogueCatalog _dialogueCatalog = new();
    private AppSettings _settings;
    private PetState _petState;
    private AssistantTimerState _assistantTimer = new();
    private PixelPoint _dragPointerStart;
    private PixelPoint _dragWindowStart;
    private IPointer? _dragPointer;
    private string? _surfaceBeforeDrag;
    private string? _dragLandingSurfaceId;
    private long _lastDragSurfacePollMs;
    private bool _dragging;
    private bool _walking;
    private PixelPoint _target;
    private long _lastMovementMs;
    private long _nextDecisionMs;
    private bool _bob;
    private bool _currentFrameFacesLeft = ProductEditionInfo.DefaultFrameFacesLeft;
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
    private bool _dialogueEditorOpen;
    private int _obstacleTurnDirection;
    private int _landingOperationId;
    private long _nextCareTickMs;
    private DateTimeOffset _lastPetStateSaveUtc = DateTimeOffset.MinValue;
    private bool _wasUserAway;
    private DateTimeOffset _lastReturnGreetingUtc = DateTimeOffset.MinValue;
    private bool _careTickRunning;

    public bool BehaviorPaused => _settings.BehaviorPaused;
    public bool ClickThrough => _settings.ClickThrough;
    public bool DoNotDisturb => _settings.DoNotDisturb;
    public bool AutoStart => _settings.AutoStart;
    public bool TimerRunning => _assistantTimer.IsRunning;
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
        _dialogueService = new CharacterDialogueService(paths, store);
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
        Title = ProductEditionInfo.LocalizeDisplayName(localization.Get("app.name"));

        foreach (var slot in CharacterImageSlots.All)
        {
            var assetName = BundledAssetName(slot);
            _bundledCharacters[slot] = new Bitmap(
                AssetLoader.Open(new Uri(
                    $"avares://{ProductEditionInfo.DesktopAssemblyName}/Assets/{assetName}")));
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
        _landingIndicator = new Border
        {
            Width = 112,
            Height = 5,
            CornerRadius = new CornerRadius(3),
            Background = new SolidColorBrush(Color.FromRgb(95, 220, 145)),
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, 0, 2),
            IsVisible = false
        };

        var canvas = new Grid();
        canvas.Children.Add(_character);
        canvas.Children.Add(_landingIndicator);
        canvas.Children.Add(_bubble);
        Content = canvas;
        ContextMenu = BuildContextMenu();

        PointerPressed += OnPointerPressed;
        PointerMoved += OnPointerMoved;
        PointerReleased += OnPointerReleased;
        KeyDown += OnKeyDown;
        Opened += async (_, _) =>
        {
            PlaceInitially();
            await LoadRegionsAsync();
            ApplySettings();
            RefreshDesktopState();
            await LoadCharacterImagesAsync();
            _assistantTimer = await _store.LoadOrCreateAsync(
                _paths.TimersFile,
                () => new AssistantTimerState());
            ContextMenu = BuildContextMenu();
            QuickSettingsChanged?.Invoke(this, EventArgs.Empty);
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
        var timers = BuildTimerMenu();

        menu.Items.Add(Item("menu.interact", () => ShowRandomGreeting()));
        menu.Items.Add(Item("menu.status", OpenPetStatus));
        menu.Items.Add(Item("menu.feed", FeedAsync));
        menu.Items.Add(Item("menu.pet", PetAsync));
        menu.Items.Add(Item("menu.play", PlayAsync));
        menu.Items.Add(Item("menu.clean", CleanAsync));
        menu.Items.Add(Item(
            _petState.Activity == ActivityState.Sleeping ? "menu.wake" : "menu.sleep",
            _petState.Activity == ActivityState.Sleeping ? WakeAsync : SleepAsync));
        menu.Items.Add(timers);
        menu.Items.Add(Item("menu.editDialogues", OpenDialogueEditor));
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

    private MenuItem BuildTimerMenu()
    {
        var timers = new MenuItem { Header = _localization.Get("menu.timer") };
        timers.Items.Add(RawItem(
            string.Format(_localization.Get("timer.preset"), 5),
            () => StartAssistantTimer(AssistantTimerKind.General, 5)));
        timers.Items.Add(RawItem(
            string.Format(_localization.Get("timer.preset"), 10),
            () => StartAssistantTimer(AssistantTimerKind.General, 10)));
        timers.Items.Add(Item("timer.focus25", () => StartAssistantTimer(AssistantTimerKind.Focus, 25)));
        timers.Items.Add(Item("timer.focus50", () => StartAssistantTimer(AssistantTimerKind.Focus, 50)));
        timers.Items.Add(Item("timer.rest5", () => StartAssistantTimer(AssistantTimerKind.Rest, 5)));
        timers.Items.Add(Item("timer.custom", OpenCustomTimer));
        if (_assistantTimer.IsRunning)
        {
            var remaining = _assistantTimer.Remaining(DateTimeOffset.UtcNow);
            timers.Items.Add(new Separator());
            timers.Items.Add(RawItem(
                string.Format(_localization.Get("timer.remaining"), FormatRemaining(remaining)),
                ShowTimerStatus));
            timers.Items.Add(Item("timer.cancel", CancelAssistantTimer));
        }
        return timers;
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
        _landingOperationId++;
        _dragging = true;
        _walking = false;
        _specialAnimationUntilMs = 0;
        _specialAnimation = [];
        _dragPointerStart = this.PointToScreen(point.Position);
        _dragWindowStart = Position;
        _surfaceBeforeDrag = _currentSurfaceId;
        _dragLandingSurfaceId = null;
        _lastDragSurfacePollMs = 0;
        _dragPointer = e.Pointer;
        e.Pointer.Capture(this);
        SetCharacterFrame(CharacterImageSlot.DragPropeller);
        _character.RenderTransform = new RotateTransform(4);
        UpdateDragLandingTarget(force: true);
        e.Handled = true;
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_dragging) return;
        var current = this.PointToScreen(e.GetPosition(this));
        Position = new PixelPoint(_dragWindowStart.X + current.X - _dragPointerStart.X, _dragWindowStart.Y + current.Y - _dragPointerStart.Y);
        _character.RenderTransform = new RotateTransform((_clock.ElapsedMilliseconds / 120) % 2 == 0 ? 4 : -4);
        UpdateDragLandingTarget(force: false);
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
        _dragPointer = null;
        _landingIndicator.IsVisible = false;
        var operationId = ++_landingOperationId;
        await LandAsync(operationId, _dragLandingSurfaceId);
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (!_dragging || e.Key != Key.Escape) return;
        _landingOperationId++;
        _dragging = false;
        _dragPointer?.Capture(null);
        _dragPointer = null;
        e.Handled = true;
        Position = _dragWindowStart;
        _landingIndicator.IsVisible = false;
        if (FindSurface(_surfaceBeforeDrag) is { } original)
            AttachToSurface(original, Position.X);
        SetCharacterFrame(CharacterImageSlot.Default);
        _character.RenderTransform = null;
    }

    private void UpdateDragLandingTarget(bool force)
    {
        var now = _clock.ElapsedMilliseconds;
        if (!force && now - _lastDragSurfacePollMs < 100) return;
        _lastDragSurfacePollMs = now;
        RefreshWindowSurfaces();
        var petWidth = PetPixelWidth();
        var petBottomCenter = new DesktopPoint(
            Position.X + petWidth / 2,
            Position.Y + PetPixelHeight());
        var target = MovementGeometry.FindDragTarget(
            GetAvailableSurfaces().Where(surface => GetWalkableRanges(surface).Count > 0),
            petBottomCenter,
            petWidth,
            _surfaceBeforeDrag);
        _dragLandingSurfaceId = target?.Id;
        _landingIndicator.IsVisible = target is not null;
        _landingIndicator.Background = new SolidColorBrush(
            target?.Kind == MovementSurfaceKind.WindowTop
                ? Color.FromRgb(95, 220, 145)
                : Color.FromRgb(100, 175, 245));
    }

    private async Task LandAsync(int operationId, string? preferredSurfaceId = null)
    {
        if (!MovementGeometry.CanContinueLanding(operationId, _landingOperationId, _dragging))
            return;

        SetCharacterFrame(CharacterImageSlot.Fall);
        _character.RenderTransform = new RotateTransform(-4);
        RefreshWindowSurfaces();
        var currentPetWidth = PetPixelWidth();
        var currentPetHeight = PetPixelHeight();
        var petBottom = Position.Y + currentPetHeight;
        var candidates = GetAvailableSurfaces()
            .Where(surface =>
            {
                var ground = surface.Kind == MovementSurfaceKind.WindowTop
                    ? surface.Bounds.Y
                    : surface.Bounds.Bottom;
                return (surface.Id == preferredSurfaceId || ground >= petBottom - 48) &&
                       GetWalkableRanges(surface).Count > 0;
            })
            .ToArray();
        var surface = candidates.FirstOrDefault(candidate => candidate.Id == preferredSurfaceId) ??
                      MovementGeometry.FindNearest(
                          candidates,
                          new DesktopPoint(Position.X + (currentPetWidth / 2), petBottom),
                          currentPetWidth);
        var landingRange = surface is null
            ? null
            : MovementGeometry.FindNearestRange(GetWalkableRanges(surface), Position.X);
        if (surface is not null &&
            landingRange is not null &&
            MovementGeometry.TryPlace(
                surface,
                Math.Clamp(Position.X, landingRange.Value.MinimumX, landingRange.Value.MaximumX),
                PetPixelWidth(surface),
                PetPixelHeight(surface),
                out var placement))
        {
            var x = (int)Math.Round(placement.X);
            var floor = (int)Math.Round(placement.Y);
            var fallStartY = Position.Y;
            var fallStep = ProductEditionInfo.IsYaroro ? 32 : 22;
            for (var y = Position.Y; y < floor; y = Math.Min(floor, y + fallStep))
            {
                if (!MovementGeometry.CanContinueLanding(operationId, _landingOperationId, _dragging))
                    return;
                Position = new PixelPoint(x, y);
                await Task.Delay(16);
            }
            if (!MovementGeometry.CanContinueLanding(operationId, _landingOperationId, _dragging))
                return;
            Position = new PixelPoint(x, floor);
            AttachToSurface(surface, x);
            if (ProductEditionInfo.IsYaroro && floor - fallStartY >= 70)
            {
                SetCharacterFrame(CharacterImageSlot.LandStunned);
                _character.RenderTransform = null;
                await Task.Delay(900);
                if (!MovementGeometry.CanContinueLanding(operationId, _landingOperationId, _dragging))
                    return;
            }
        }
        if (MovementGeometry.CanContinueLanding(operationId, _landingOperationId, _dragging))
        {
            SetCharacterFrame(CharacterImageSlot.Default);
            _character.RenderTransform = null;
        }
    }

    private void UpdateMovement()
    {
        var now = _clock.ElapsedMilliseconds;
        var elapsed = Math.Clamp((now - _lastMovementMs) / 1000d, 0, 0.1);
        _lastMovementMs = now;
        if (_desktopIntegration.IsClickThroughHotKeyPressed())
            ToggleClickThrough();

        if (MovementGeometry.ShouldSynchronizeAttachedSurface(_dragging) &&
            now >= _nextDesktopPollMs)
        {
            _nextDesktopPollMs = now + (_walking ? 500 : 1000);
            RefreshDesktopState();
        }
        if (now >= _nextSettingsPollMs)
        {
            _nextSettingsPollMs = now + 1000;
            _ = ReloadExternalSettingsAsync();
        }
        if (now >= _nextCareTickMs)
        {
            _nextCareTickMs = now + 1000;
            _ = TickCareAndTimerAsync();
        }

        var inactive = _dragging || _transitioning || _recovering || _dialogueEditorOpen ||
                       _waitingAtFullScreenEdge ||
                       now < _specialAnimationUntilMs ||
                       _petState.Activity == ActivityState.Sleeping ||
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
            if (_obstacleTurnDirection != 0 &&
                FindSurface(_currentSurfaceId) is { } obstacleSurface)
            {
                var direction = _obstacleTurnDirection;
                _obstacleTurnDirection = 0;
                if (ChooseTargetAwayFromObstacle(obstacleSurface, direction))
                    return;
            }
            _walking = false;
            _nextDecisionMs = now + Random.Shared.Next(5000, 11000);
            return;
        }

        var step = Math.Min(distance, PixelsPerSecond() * elapsed);
        Position = new PixelPoint((int)(Position.X + dx / distance * step), (int)(Position.Y + dy / distance * step));
        if (FindSurface(_currentSurfaceId) is { } current)
            _surfaceRelativeX = MovementGeometry.RelativeX(current, Position.X, PetPixelWidth(current));
        var movingLeft = dx < 0;
        _character.RenderTransform =
            new ScaleTransform(MovementGeometry.HorizontalScale(movingLeft, _currentFrameFacesLeft), 1);
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
        if (_waitingAtFullScreenEdge)
        {
            _animationTimer.Interval = TimeSpan.FromMilliseconds(1000);
            _character.Margin = default;
            _character.RenderTransform = null;
            SetCharacterFrame(CharacterImageSlot.Default);
            return;
        }
        if (_petState.Activity == ActivityState.Sleeping)
        {
            _animationTimer.Interval = TimeSpan.FromMilliseconds(850);
            _character.Margin = default;
            SetCharacterFrame(
                _specialAnimationFrame++ % 2 == 0
                    ? CharacterImageSlot.Sleep1
                    : CharacterImageSlot.Sleep2);
            return;
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
            await LoadDialoguesAsync();
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
        Bitmap? selected;
        if (_characterBitmaps.TryGetValue(slot, out selected) ||
            _characterBitmaps.TryGetValue(CharacterImageSlot.Default, out selected))
        {
            _currentFrameFacesLeft = false;
        }
        else if (_bundledCharacters.TryGetValue(slot, out selected) ||
                 _bundledCharacters.TryGetValue(CharacterImageSlot.Default, out selected))
        {
            _currentFrameFacesLeft = ProductEditionInfo.DefaultFrameFacesLeft;
        }
        else
        {
            return;
        }

        if (!ReferenceEquals(_character.Source, selected)) _character.Source = selected;
    }

    private static string BundledAssetName(CharacterImageSlot slot) => slot switch
    {
        CharacterImageSlot.Default => "character-default.png",
        CharacterImageSlot.Back => "character-back.png",
        CharacterImageSlot.Walk1 => "character-walk-1.png",
        CharacterImageSlot.Walk2 => "character-walk-2.png",
        CharacterImageSlot.Walk3 => "character-walk-3.png",
        CharacterImageSlot.Eat1 => "character-eat-1.png",
        CharacterImageSlot.Eat2 => "character-eat-2.png",
        CharacterImageSlot.Sleep1 => "character-sleep-1.png",
        CharacterImageSlot.Sleep2 => "character-sleep-2.png",
        CharacterImageSlot.DragPropeller => "character-drag-propeller.png",
        CharacterImageSlot.Fall => "character-fall.png",
        CharacterImageSlot.LandStunned => "character-land-stunned.png",
        _ => "character-default.png"
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
        _obstacleTurnDirection = 0;
        var available = GetAvailableSurfaces()
            .Where(surface => GetWalkableRanges(surface).Count > 0)
            .ToArray();
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
        var ranges = GetWalkableRanges(surface);
        var range = current?.Id == surface.Id
            ? MovementGeometry.FindContainingRange(ranges, Position.X) ??
              MovementGeometry.FindNearestRange(ranges, Position.X)
            : ranges[Random.Shared.Next(ranges.Count)];
        if (range is null) return;
        var requestedX = range.Value.MinimumX +
                         (Random.Shared.NextDouble() * (range.Value.MaximumX - range.Value.MinimumX));
        if (!MovementGeometry.TryPlace(
                surface,
                requestedX,
                PetPixelWidth(surface),
                PetPixelHeight(surface),
                out var placement)) return;
        _currentSurfaceId = surface.Id;
        _targetRelativeX = MovementGeometry.RelativeX(surface, placement.X, PetPixelWidth(surface));
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
            var ranges = GetWalkableRanges(surface);
            if (ranges.Count == 0) return;
            var enterFromLeft = Random.Shared.Next(2) == 0;
            var range = enterFromLeft ? ranges[0] : ranges[^1];
            var edgeX = enterFromLeft ? range.MinimumX : range.MaximumX;
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
        RefreshWindowSurfaces();
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

        var petWidth = PetPixelWidth(current);
        var ranges = GetWalkableRanges(current);
        if (ranges.Count == 0)
        {
            RecoverToNearestSurface();
            return;
        }

        var currentRequestedX = MovementGeometry.ResolveRelativeX(current, _surfaceRelativeX, petWidth);
        var currentRange = MovementGeometry.FindContainingRange(ranges, currentRequestedX);
        if (currentRange is null)
        {
            var nearestRange = MovementGeometry.FindNearestRange(ranges, currentRequestedX);
            if (nearestRange is null)
            {
                RecoverToNearestSurface();
                return;
            }

            var safeX = Math.Clamp(currentRequestedX, nearestRange.Value.MinimumX, nearestRange.Value.MaximumX);
            if (!MovementGeometry.TryPlace(
                    current,
                    safeX,
                    petWidth,
                    PetPixelHeight(current),
                    out var safePlacement))
            {
                RecoverToNearestSurface();
                return;
            }

            Position = new PixelPoint((int)Math.Round(safePlacement.X), (int)Math.Round(safePlacement.Y));
            _surfaceRelativeX = MovementGeometry.RelativeX(current, safePlacement.X, petWidth);
            var direction = safeX <= currentRequestedX ? -1 : 1;
            _obstacleTurnDirection = 0;
            if (!ChooseTargetAwayFromObstacle(current, direction))
            {
                _walking = false;
                _nextDecisionMs = _clock.ElapsedMilliseconds + 1200;
            }
            return;
        }

        var requestedX = _walking
            ? MovementGeometry.ResolveRelativeX(current, _targetRelativeX, petWidth)
            : currentRequestedX;
        var clampedX = Math.Clamp(
            requestedX,
            currentRange.Value.MinimumX,
            currentRange.Value.MaximumX);
        if (_walking)
            _obstacleTurnDirection = Math.Abs(clampedX - requestedX) > 0.5
                ? requestedX > clampedX ? -1 : 1
                : 0;
        else
            _obstacleTurnDirection = 0;
        if (!MovementGeometry.TryPlace(
                current,
                clampedX,
                petWidth,
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

    private void RefreshWindowSurfaces()
    {
        _windowSurfaces = _desktopIntegration.GetWindowSurfaces(_settings.ExcludedWindowProcesses);
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
            _nextDecisionMs = _clock.ElapsedMilliseconds + 1800;
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
        _character.Margin = default;
        _character.RenderTransform = null;
        SetCharacterFrame(CharacterImageSlot.Default);
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
            surfaces.AddRange(_windowSurfaces.Where(surface => surface.IsWalkable));

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

    private IReadOnlyList<WalkableRange> GetWalkableRanges(MovementSurface surface) =>
        MovementGeometry.GetWalkableRanges(surface, _windowSurfaces, PetPixelWidth(surface));

    private bool ChooseTargetAwayFromObstacle(MovementSurface surface, int direction)
    {
        var ranges = GetWalkableRanges(surface);
        var range = MovementGeometry.FindContainingRange(ranges, Position.X) ??
                    MovementGeometry.FindNearestRange(ranges, Position.X);
        if (range is null) return false;

        const double minimumTurnDistance = 24;
        var currentX = Math.Clamp(Position.X, range.Value.MinimumX, range.Value.MaximumX);
        double targetX;
        if (direction < 0)
        {
            if (currentX - range.Value.MinimumX < minimumTurnDistance) return false;
            targetX = range.Value.MinimumX +
                      Random.Shared.NextDouble() * Math.Max(0, currentX - range.Value.MinimumX - minimumTurnDistance);
        }
        else
        {
            if (range.Value.MaximumX - currentX < minimumTurnDistance) return false;
            targetX = currentX + minimumTurnDistance +
                      Random.Shared.NextDouble() * Math.Max(0, range.Value.MaximumX - currentX - minimumTurnDistance);
        }

        _targetRelativeX = MovementGeometry.RelativeX(surface, targetX, PetPixelWidth(surface));
        _target = new PixelPoint(
            (int)Math.Round(targetX),
            (int)Math.Round(surface.PetTop(PetPixelHeight(surface))));
        _walking = true;
        return true;
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
        var surfaces = GetAvailableSurfaces()
            .Where(surface => GetWalkableRanges(surface).Count > 0)
            .ToArray();
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
        var landingRange = MovementGeometry.FindNearestRange(GetWalkableRanges(nearest), Position.X);
        if (landingRange is null ||
            !MovementGeometry.TryPlace(
                nearest,
                Math.Clamp(Position.X, landingRange.Value.MinimumX, landingRange.Value.MaximumX),
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

    private double PixelsPerSecond()
    {
        var baseSpeed = _settings.MovementSpeed switch
        {
            MovementSpeed.VerySlow => 18,
            MovementSpeed.Slow => 30,
            MovementSpeed.Normal => 48,
            MovementSpeed.Fast => 75,
            _ => Math.Clamp(_settings.CustomPixelsPerSecond, 5, 240)
        };
        return _petState.Mood is MoodState.Tired or MoodState.Hungry ? baseSpeed * 0.72 : baseSpeed;
    }

    private void ShowRandomGreeting() => ShowDialogue(DialogueGroupIds.Click);

    private string ActiveDialogueCharacterId =>
        _characterBitmaps.Count > 0 ? "user-character" : ProductEditionInfo.DefaultCharacterId;

    private async Task LoadDialoguesAsync()
    {
        var characterId = ActiveDialogueCharacterId;
        var language = _localization.Language;
        try
        {
            _dialogueCatalog = await _dialogueService.LoadAsync(
                characterId,
                language,
                () => CharacterDialogueService.CreateDefaults(
                    characterId,
                    language,
                    key => _localization.Get(key)));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            _dialogueCatalog = CharacterDialogueService.CreateDefaults(
                characterId,
                language,
                key => _localization.Get(key));
        }
    }

    private void ShowDialogue(string groupId)
    {
        var selected = _dialogues.Select(
            _dialogueCatalog.GetGroup(groupId),
            _petState,
            DateTimeOffset.UtcNow);
        if (selected is not null) ShowBubble(ExpandDialogueVariables(selected.Text));
    }

    private static string ExpandDialogueVariables(string text) =>
        text.Replace(
            "{time}",
            DateTime.Now.ToString("t", System.Globalization.CultureInfo.CurrentCulture),
            StringComparison.OrdinalIgnoreCase);

    private void OpenDialogueEditor()
    {
        if (_dialogueEditorOpen) return;
        _dialogueEditorOpen = true;
        _walking = false;
        var editor = new DialogueEditorWindow(
            _paths,
            _store,
            _localization,
            ActiveDialogueCharacterId,
            text => ShowBubble(ExpandDialogueVariables(text)));
        editor.DialoguesSaved += async (_, _) => await LoadDialoguesAsync();
        editor.Closed += async (_, _) =>
        {
            _dialogueEditorOpen = false;
            _nextDecisionMs = _clock.ElapsedMilliseconds + 1500;
            await LoadDialoguesAsync();
        };
        editor.Show(this);
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
        ShowDialogue(DialogueGroupIds.Feed);
        await SavePetStateAsync();
    }

    private async void PetAsync()
    {
        _petState = _petStateService.Pet(_petState, DateTimeOffset.UtcNow);
        _character.RenderTransform = new RotateTransform(-5);
        ShowBubble(_localization.Get("interaction.pet"));
        await Task.Delay(350);
        _character.RenderTransform = null;
        await SavePetStateAsync();
    }

    private async void CleanAsync()
    {
        _petState = _petStateService.Clean(_petState, DateTimeOffset.UtcNow);
        ShowBubble(_localization.Get("interaction.clean"));
        await Task.Delay(600);
        _petState = _petState with { Activity = ActivityState.Idle };
        await SavePetStateAsync();
    }

    private async void SleepAsync()
    {
        _petState = _petStateService.Sleep(_petState, DateTimeOffset.UtcNow);
        _walking = false;
        ShowDialogue(DialogueGroupIds.Sleep);
        ContextMenu = BuildContextMenu();
        await SavePetStateAsync();
    }

    private async void WakeAsync()
    {
        _petState = _petStateService.Wake(_petState, DateTimeOffset.UtcNow);
        SetCharacterFrame(CharacterImageSlot.Default);
        _nextDecisionMs = _clock.ElapsedMilliseconds + 1800;
        ShowBubble(_localization.Get("interaction.wake"));
        ContextMenu = BuildContextMenu();
        await SavePetStateAsync();
    }

    private async void PlayAsync()
    {
        _petState = _petStateService.Play(_petState, DateTimeOffset.UtcNow);
        ShowDialogue(DialogueGroupIds.Play);
        await Task.Delay(900);
        _petState = _petState with { Activity = ActivityState.Idle };
        await SavePetStateAsync();
    }

    private void OpenPetStatus()
    {
        var status = new PetStatusWindow(_petState, _localization) { Topmost = _settings.AlwaysOnTop };
        status.Show(this);
    }

    private async void StartAssistantTimer(AssistantTimerKind kind, int minutes)
    {
        _assistantTimer = _assistantTimerService.Start(kind, minutes, DateTimeOffset.UtcNow);
        await _store.SaveAsync(_paths.TimersFile, _assistantTimer);
        ContextMenu = BuildContextMenu();
        QuickSettingsChanged?.Invoke(this, EventArgs.Empty);
        ShowBubble(string.Format(_localization.Get("timer.started"), minutes));
    }

    private async void CancelAssistantTimer()
    {
        _assistantTimer = _assistantTimerService.Cancel();
        await _store.SaveAsync(_paths.TimersFile, _assistantTimer);
        ContextMenu = BuildContextMenu();
        QuickSettingsChanged?.Invoke(this, EventArgs.Empty);
        ShowBubble(_localization.Get("timer.cancelled"));
    }

    private async void OpenCustomTimer()
    {
        var setup = new TimerSetupWindow(_localization) { Topmost = _settings.AlwaysOnTop };
        var request = await setup.ShowDialog<TimerRequest?>(this);
        if (request is not null) StartAssistantTimer(request.Kind, request.Minutes);
    }

    public void ShowTimerStatus()
    {
        if (!_assistantTimer.IsRunning)
        {
            ShowBubble(_localization.Get("timer.none"));
            return;
        }
        ShowBubble(string.Format(
            _localization.Get("timer.remainingBubble"),
            FormatRemaining(_assistantTimer.Remaining(DateTimeOffset.UtcNow))));
    }

    private static string FormatRemaining(TimeSpan remaining)
    {
        if (remaining < TimeSpan.Zero) remaining = TimeSpan.Zero;
        return remaining.TotalHours >= 1
            ? $"{(int)remaining.TotalHours}:{remaining.Minutes:00}:{remaining.Seconds:00}"
            : $"{(int)remaining.TotalMinutes}:{remaining.Seconds:00}";
    }

    private async Task TickCareAndTimerAsync()
    {
        if (_careTickRunning) return;
        _careTickRunning = true;
        try
        {
            var now = DateTimeOffset.UtcNow;
            _petState = _petStateService.ApplyElapsed(_petState, now);
            var idle = _desktopIntegration.GetIdleTime();
            if (idle >= TimeSpan.FromMinutes(5))
            {
                if (!_wasUserAway)
                {
                    _wasUserAway = true;
                    _petState = _petStateService.Sleep(_petState, now);
                    _walking = false;
                    ContextMenu = BuildContextMenu();
                }
            }
            else if (_wasUserAway && idle <= TimeSpan.FromSeconds(8))
            {
                _wasUserAway = false;
                _petState = _petStateService.Wake(_petState, now);
                ContextMenu = BuildContextMenu();
                if (now - _lastReturnGreetingUtc >= TimeSpan.FromMinutes(30))
                {
                    _lastReturnGreetingUtc = now;
                    ShowBubble(_localization.Get("interaction.return"));
                }
            }

            if (_assistantTimerService.IsComplete(_assistantTimer, now))
            {
                var completedKind = _assistantTimer.Kind;
                _assistantTimer = _assistantTimerService.Cancel();
                await _store.SaveAsync(_paths.TimersFile, _assistantTimer);
                ContextMenu = BuildContextMenu();
                QuickSettingsChanged?.Invoke(this, EventArgs.Empty);
                ShowBubble(_localization.Get($"timer.finished.{completedKind.ToString().ToLowerInvariant()}"));
            }

            if (now - _lastPetStateSaveUtc >= TimeSpan.FromMinutes(1))
                await SavePetStateAsync();
        }
        finally
        {
            _careTickRunning = false;
        }
    }

    private async Task SavePetStateAsync()
    {
        await _store.SaveAsync(_paths.PetStateFile, _petState);
        _lastPetStateSaveUtc = DateTimeOffset.UtcNow;
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
        Title = ProductEditionInfo.LocalizeDisplayName(_localization.Get("app.name"));
        ContextMenu = BuildContextMenu();
        await LoadDialoguesAsync();
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
        var name = OperatingSystem.IsWindows()
            ? ProductEditionInfo.ConfigExecutableName
            : Path.GetFileNameWithoutExtension(ProductEditionInfo.ConfigExecutableName);
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
        var installedUpdater = Path.Combine(AppContext.BaseDirectory, ProductEditionInfo.UpdaterExecutableName);
        if (!File.Exists(installedUpdater))
        {
            ShowBubble(_localization.Get("update.updaterMissing"));
            return;
        }

        try
        {
            var runnerDirectory = Path.Combine(_paths.Updates, "runner");
            Directory.CreateDirectory(runnerDirectory);
            var runner = Path.Combine(runnerDirectory, ProductEditionInfo.UpdaterExecutableName);
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
