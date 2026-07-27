using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using PixelCompanion.Core.Models;
using PixelCompanion.Core.Services;

namespace PixelCompanion.Config;

public sealed class ConfigWindow : Window
{
    private readonly AppPaths _paths = new();
    private readonly AtomicJsonStore _store = new();
    private readonly ComboBox _language = new();
    private readonly ComboBox _speed = new();
    private readonly ComboBox _movementSurface = new();
    private readonly ComboBox _fullScreenBehavior = new();
    private readonly NumericUpDown _scale = new() { Minimum = 1, Maximum = 6, Increment = 1 };
    private readonly CheckBox _topmost = new() { Content = "Always on top / 항상 위" };
    private readonly CheckBox _clickThrough = new() { Content = "Click-through (Ctrl+Alt+P to toggle) / 클릭 통과 (Ctrl+Alt+P로 전환)" };
    private readonly CheckBox _autoStart = new() { Content = "Start at login / 로그인 시 자동 시작" };
    private readonly CheckBox _includeSystemAreas = new() { Content = "Include taskbar area / 작업 표시줄 영역 포함" };
    private readonly CheckBox _sound = new() { Content = "Sound / 소리" };
    private readonly CheckBox _dnd = new() { Content = "Do not disturb / 방해 금지" };
    private readonly CheckBox _autoCheckUpdates = new() { Content = "Automatically check for updates / 자동으로 업데이트 확인" };
    private readonly TextBox _excludedProcesses = new()
    {
        PlaceholderText = "game.exe, presentation-app / 제외할 프로세스 이름(쉼표로 구분)"
    };
    private readonly TextBlock _status = new();
    private readonly ListBox _validation = new();
    private readonly Dictionary<CharacterImageSlot, Image> _characterPreviews = [];
    private readonly Dictionary<CharacterImageSlot, TextBlock> _characterFileNames = [];
    private readonly Dictionary<CharacterImageSlot, Bitmap> _previewBitmaps = [];
    private readonly UserCharacterService _characterService;
    private AppSettings _settings = new();
    private bool _dirty;

    public ConfigWindow()
    {
        Title = "Pixel Companion — Advanced Settings";
        Width = 820;
        Height = 600;
        MinWidth = 680;
        MinHeight = 480;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        _paths.EnsureCreated();
        _characterService = new UserCharacterService(_paths, _store);

        var header = new TextBlock
        {
            Text = "Pixel Companion",
            FontSize = 24,
            FontWeight = FontWeight.SemiBold,
            Margin = new Thickness(0, 0, 0, 14)
        };
        var tabs = new TabControl();
        tabs.Items.Add(new TabItem { Header = "General / 일반", Content = BuildGeneralPanel() });
        tabs.Items.Add(new TabItem { Header = "Character / 캐릭터", Content = BuildCharacterPanel() });
        tabs.Items.Add(new TabItem { Header = "Status / 상태", Content = BuildStatusPanel() });

        var save = new Button { Content = "Save / 저장", HorizontalAlignment = HorizontalAlignment.Right, MinWidth = 100 };
        save.Click += async (_, _) => await SaveAsync();
        var root = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto"), Margin = new Thickness(24) };
        Grid.SetRow(header, 0);
        Grid.SetRow(tabs, 1);
        Grid.SetRow(save, 2);
        root.Children.Add(header);
        root.Children.Add(tabs);
        root.Children.Add(save);
        Content = root;

        Opened += async (_, _) => await LoadAsync();
        Closing += OnClosing;
    }

    private Control BuildGeneralPanel()
    {
        _language.Items.Add("Automatic / 자동");
        _language.Items.Add("English");
        _language.Items.Add("한국어");
        foreach (var value in Enum.GetNames<MovementSpeed>()) _speed.Items.Add(value);
        foreach (var value in Enum.GetNames<MovementSurfaceMode>()) _movementSurface.Items.Add(value);
        foreach (var value in Enum.GetNames<FullScreenBehavior>()) _fullScreenBehavior.Items.Add(value);
        _language.SelectionChanged += (_, _) => _dirty = true;
        _speed.SelectionChanged += (_, _) => _dirty = true;
        _movementSurface.SelectionChanged += (_, _) => _dirty = true;
        _fullScreenBehavior.SelectionChanged += (_, _) => _dirty = true;
        _scale.ValueChanged += (_, _) => _dirty = true;
        _topmost.IsCheckedChanged += (_, _) => _dirty = true;
        _clickThrough.IsCheckedChanged += (_, _) => _dirty = true;
        _autoStart.IsCheckedChanged += (_, _) => _dirty = true;
        _includeSystemAreas.IsCheckedChanged += (_, _) => _dirty = true;
        _sound.IsCheckedChanged += (_, _) => _dirty = true;
        _dnd.IsCheckedChanged += (_, _) => _dirty = true;
        _autoCheckUpdates.IsCheckedChanged += (_, _) => _dirty = true;
        _excludedProcesses.TextChanged += (_, _) => _dirty = true;

        var panel = FormPanel();
        panel.Children.Add(Label("UI language / UI 언어", _language));
        panel.Children.Add(Label("Movement speed / 이동 속도", _speed));
        panel.Children.Add(Label("Walking surfaces / 이동 지면", _movementSurface));
        panel.Children.Add(Label("When full screen / 전체 화면 동작", _fullScreenBehavior));
        panel.Children.Add(Label("Integer scale / 정수 배율", _scale));
        panel.Children.Add(Label("Excluded window apps / 창 지면 제외 앱", _excludedProcesses));
        panel.Children.Add(_topmost);
        panel.Children.Add(_clickThrough);
        panel.Children.Add(_autoStart);
        panel.Children.Add(_includeSystemAreas);
        panel.Children.Add(_sound);
        panel.Children.Add(_dnd);
        panel.Children.Add(_autoCheckUpdates);
        return new ScrollViewer { Content = panel };
    }

    private Control BuildCharacterPanel()
    {
        var info = new TextBlock
        {
            Text = "Drop an image onto a slot or click Choose. PNG, JPG, JPEG, and GIF files up to 20 MB are supported. GIF uses its first frame. Missing walking, eating, or sleeping images fall back to Default.\n\n이미지를 각 칸에 끌어 놓거나 선택 버튼을 누르세요. 20MB 이하 PNG, JPG, JPEG, GIF를 지원하며 GIF는 첫 프레임을 사용합니다. 빠진 걷기·밥 먹기·잠자기 이미지는 기본 이미지로 자연스럽게 대체됩니다.",
            TextWrapping = TextWrapping.Wrap
        };
        var imageSlots = new WrapPanel { Orientation = Orientation.Horizontal };
        imageSlots.Children.Add(BuildImageSlot(CharacterImageSlot.Default, "Default / 기본"));
        imageSlots.Children.Add(BuildImageSlot(CharacterImageSlot.Back, "Back / 뒷모습"));
        imageSlots.Children.Add(BuildImageSlot(CharacterImageSlot.Walk1, "Walk 1 / 걷기 1"));
        imageSlots.Children.Add(BuildImageSlot(CharacterImageSlot.Walk2, "Walk 2 / 걷기 2"));
        imageSlots.Children.Add(BuildImageSlot(CharacterImageSlot.Walk3, "Walk 3 / 걷기 3"));
        imageSlots.Children.Add(BuildImageSlot(CharacterImageSlot.Eat1, "Eat 1 / 밥 먹기 1"));
        imageSlots.Children.Add(BuildImageSlot(CharacterImageSlot.Eat2, "Eat 2 / 밥 먹기 2"));
        imageSlots.Children.Add(BuildImageSlot(CharacterImageSlot.Sleep1, "Sleep 1 / 잠자기 1"));
        imageSlots.Children.Add(BuildImageSlot(CharacterImageSlot.Sleep2, "Sleep 2 / 잠자기 2"));
        var validate = new Button { Content = "Validate bundled character / 기본 캐릭터 검사", HorizontalAlignment = HorizontalAlignment.Left };
        validate.Click += async (_, _) => await ValidateBundledCharacterAsync();
        var panel = FormPanel();
        panel.Children.Add(info);
        panel.Children.Add(imageSlots);
        panel.Children.Add(validate);
        panel.Children.Add(_validation);
        return new ScrollViewer { Content = panel };
    }

    private Control BuildImageSlot(CharacterImageSlot slot, string title)
    {
        var preview = new Image
        {
            Width = 112,
            Height = 112,
            Stretch = Stretch.Uniform,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        RenderOptions.SetBitmapInterpolationMode(preview, BitmapInterpolationMode.None);
        var fileName = new TextBlock
        {
            Text = "Not set / 미설정",
            TextAlignment = TextAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxWidth = 160
        };
        _characterPreviews[slot] = preview;
        _characterFileNames[slot] = fileName;

        var choose = new Button
        {
            Content = "Choose / 선택",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            MinHeight = 32
        };
        choose.Click += async (_, _) => await ChooseCharacterImageAsync(slot);
        var remove = new Button
        {
            Content = "Remove / 제거",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            MinHeight = 32
        };
        remove.Click += async (_, _) =>
        {
            await _characterService.RemoveAsync(slot);
            _dirty = true;
            await RefreshCharacterImagesAsync();
        };

        var buttons = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 6,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Children = { choose, remove }
        };
        var content = new StackPanel
        {
            Spacing = 8,
            Children =
            {
                new TextBlock { Text = title, FontWeight = FontWeight.SemiBold, TextAlignment = TextAlignment.Center },
                preview,
                fileName,
                buttons
            }
        };
        var card = new Border
        {
            Width = 185,
            Margin = new Thickness(0, 0, 10, 10),
            Padding = new Thickness(10),
            BorderThickness = new Thickness(1),
            BorderBrush = Brushes.Gray,
            CornerRadius = new CornerRadius(6),
            Child = content
        };
        DragDrop.SetAllowDrop(card, true);
        DragDrop.AddDragOverHandler(card, (_, e) =>
        {
            e.DragEffects = e.DataTransfer.Formats.Contains(DataFormat.File)
                ? DragDropEffects.Copy
                : DragDropEffects.None;
        });
        DragDrop.AddDropHandler(card, async (_, e) =>
        {
            var file = e.DataTransfer.TryGetFiles()?.FirstOrDefault();
            if (file is not null)
                await ImportCharacterImageAsync(slot, file.Path.LocalPath);
        });
        return card;
    }

    private async Task ChooseCharacterImageAsync(CharacterImageSlot slot)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Choose character image / 캐릭터 이미지 선택",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Character images")
                {
                    Patterns = ["*.png", "*.jpg", "*.jpeg", "*.gif"],
                    MimeTypes = ["image/png", "image/jpeg", "image/gif"]
                }
            ]
        });
        var file = files.FirstOrDefault();
        if (file is not null)
            await ImportCharacterImageAsync(slot, file.Path.LocalPath);
    }

    private async Task ImportCharacterImageAsync(CharacterImageSlot slot, string path)
    {
        var result = await _characterService.ImportAsync(slot, path);
        if (!result.Success)
        {
            _validation.ItemsSource = new[] { $"Image import failed / 이미지 가져오기 실패: {result.Error}" };
            return;
        }

        _settings = _settings with { ActiveCharacterId = "user-character" };
        _dirty = true;
        _validation.ItemsSource = new[] { "Image imported / 이미지를 가져왔습니다." };
        await RefreshCharacterImagesAsync();
    }

    private async Task RefreshCharacterImagesAsync()
    {
        var profile = await _characterService.LoadAsync();
        foreach (var slot in CharacterImageSlots.All)
        {
            if (_previewBitmaps.Remove(slot, out var previous)) previous.Dispose();
            _characterPreviews[slot].Source = null;
            var path = _characterService.ResolvePath(profile, slot);
            _characterFileNames[slot].Text = path is null
                ? "Not set / 미설정"
                : Path.GetFileName(path);
            if (path is null) continue;

            try
            {
                var bitmap = new Bitmap(path);
                _previewBitmaps[slot] = bitmap;
                _characterPreviews[slot].Source = bitmap;
            }
            catch (Exception ex) when (ex is IOException or ArgumentException)
            {
                _characterFileNames[slot].Text = "Unreadable image / 읽을 수 없는 이미지";
            }
        }
    }

    private Control BuildStatusPanel()
    {
        _status.TextWrapping = TextWrapping.Wrap;
        var refresh = new Button { Content = "Refresh / 새로 고침", HorizontalAlignment = HorizontalAlignment.Left };
        refresh.Click += async (_, _) => await RefreshStatusAsync();
        var panel = FormPanel();
        panel.Children.Add(_status);
        panel.Children.Add(refresh);
        return panel;
    }

    private static StackPanel FormPanel() => new() { Spacing = 14, Margin = new Thickness(16) };
    private static Control Label(string text, Control control)
    {
        var panel = new Grid { ColumnDefinitions = new ColumnDefinitions("220,*") };
        var label = new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(control, 1);
        panel.Children.Add(label);
        panel.Children.Add(control);
        return panel;
    }

    private async Task LoadAsync()
    {
        _settings = await _store.LoadOrCreateAsync(_paths.SettingsFile, () => new AppSettings());
        _language.SelectedIndex = _settings.Language switch { "en" => 1, "ko" => 2, _ => 0 };
        _speed.SelectedItem = _settings.MovementSpeed.ToString();
        _movementSurface.SelectedItem = _settings.MovementSurfaceMode.ToString();
        _fullScreenBehavior.SelectedItem = _settings.FullScreenBehavior.ToString();
        _scale.Value = (decimal)_settings.CharacterScale;
        _topmost.IsChecked = _settings.AlwaysOnTop;
        _clickThrough.IsChecked = _settings.ClickThrough;
        _autoStart.IsChecked = _settings.AutoStart;
        _includeSystemAreas.IsChecked = _settings.IncludeSystemAreas;
        _sound.IsChecked = _settings.SoundEnabled;
        _dnd.IsChecked = _settings.DoNotDisturb;
        _autoCheckUpdates.IsChecked = _settings.AutoCheckUpdates;
        _excludedProcesses.Text = string.Join(", ", _settings.ExcludedWindowProcesses);
        _dirty = false;
        await RefreshCharacterImagesAsync();
        await RefreshStatusAsync();
    }

    private async Task SaveAsync()
    {
        _settings = _settings with
        {
            Language = _language.SelectedIndex switch { 1 => "en", 2 => "ko", _ => "auto" },
            MovementSpeed = Enum.TryParse<MovementSpeed>(_speed.SelectedItem?.ToString(), out var speed) ? speed : MovementSpeed.Slow,
            MovementSurfaceMode = Enum.TryParse<MovementSurfaceMode>(_movementSurface.SelectedItem?.ToString(), out var surface)
                ? surface
                : MovementSurfaceMode.DesktopAndWindows,
            FullScreenBehavior = Enum.TryParse<FullScreenBehavior>(_fullScreenBehavior.SelectedItem?.ToString(), out var fullScreen)
                ? fullScreen
                : FullScreenBehavior.Hide,
            CharacterScale = (double)(_scale.Value ?? 2),
            AlwaysOnTop = _topmost.IsChecked == true,
            ClickThrough = _clickThrough.IsChecked == true,
            AutoStart = _autoStart.IsChecked == true,
            IncludeSystemAreas = _includeSystemAreas.IsChecked == true,
            SoundEnabled = _sound.IsChecked == true,
            DoNotDisturb = _dnd.IsChecked == true,
            AutoCheckUpdates = _autoCheckUpdates.IsChecked == true,
            ExcludedWindowProcesses = (_excludedProcesses.Text ?? "")
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray()
        };
        await _store.SaveAsync(_paths.SettingsFile, _settings);
        _dirty = false;
        await RefreshStatusAsync();
    }

    private async Task RefreshStatusAsync()
    {
        var state = await _store.LoadOrCreateAsync(_paths.PetStateFile, () => new PetState());
        _status.Text = $"User data / 사용자 데이터: {_paths.Root}\n\nHunger / 배고픔: {state.Hunger:0}\nCleanliness / 청결도: {state.Cleanliness:0}\nHappiness / 행복도: {state.Happiness:0}\nFatigue / 피로도: {state.Fatigue:0}\nAffection / 친밀도: {state.Affection:0}";
    }

    private async Task ValidateBundledCharacterAsync()
    {
        var root = FindBundledCharacterRoot();
        if (root is null)
        {
            _validation.ItemsSource = new[] { "Bundled character source not found in this published layout." };
            return;
        }

        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true };
        var manifest = JsonSerializer.Deserialize<CharacterManifest>(await File.ReadAllTextAsync(Path.Combine(root, "character.json")), options)!;
        var catalog = JsonSerializer.Deserialize<AnimationCatalog>(await File.ReadAllTextAsync(Path.Combine(root, "animations.json")), options)!;
        var issues = new CharacterPackValidator().Validate(manifest, catalog, root);
        _validation.ItemsSource = issues.Count == 0 ? new[] { "✓ Character pack is valid / 캐릭터 팩이 유효합니다." } : issues.Select(x => $"{(x.IsError ? "✕" : "!")} {x.Message}");
    }

    private static string? FindBundledCharacterRoot()
    {
        var candidate = Path.Combine(AppContext.BaseDirectory, "characters", "Yaroro");
        return Directory.Exists(candidate) ? candidate : null;
    }

    private async void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (!_dirty) return;
        e.Cancel = true;
        await SaveAsync();
        _dirty = false;
        Close();
    }
}
