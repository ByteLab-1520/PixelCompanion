using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using PixelCompanion.Core.Models;
using PixelCompanion.Core.Services;

namespace PixelCompanion.Config;

public sealed class ConfigWindow : Window
{
    private readonly AppPaths _paths = new();
    private readonly AtomicJsonStore _store = new();
    private readonly ComboBox _language = new();
    private readonly ComboBox _speed = new();
    private readonly NumericUpDown _scale = new() { Minimum = 1, Maximum = 6, Increment = 1 };
    private readonly CheckBox _topmost = new() { Content = "Always on top / 항상 위" };
    private readonly CheckBox _sound = new() { Content = "Sound / 소리" };
    private readonly CheckBox _dnd = new() { Content = "Do not disturb / 방해 금지" };
    private readonly TextBlock _status = new();
    private readonly ListBox _validation = new();
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
        _language.SelectionChanged += (_, _) => _dirty = true;
        _speed.SelectionChanged += (_, _) => _dirty = true;
        _scale.ValueChanged += (_, _) => _dirty = true;
        _topmost.IsCheckedChanged += (_, _) => _dirty = true;
        _sound.IsCheckedChanged += (_, _) => _dirty = true;
        _dnd.IsCheckedChanged += (_, _) => _dirty = true;

        var panel = FormPanel();
        panel.Children.Add(Label("UI language / UI 언어", _language));
        panel.Children.Add(Label("Movement speed / 이동 속도", _speed));
        panel.Children.Add(Label("Integer scale / 정수 배율", _scale));
        panel.Children.Add(_topmost);
        panel.Children.Add(_sound);
        panel.Children.Add(_dnd);
        return new ScrollViewer { Content = panel };
    }

    private Control BuildCharacterPanel()
    {
        var info = new TextBlock
        {
            Text = "Character packs are stored in the shared user-data folder. Required animations are validated; optional animations safely fall back to Idle.\n\n캐릭터 팩은 공용 사용자 데이터 폴더에 저장됩니다. 필수 애니메이션은 검사하며 선택 애니메이션은 Idle로 안전하게 대체됩니다.",
            TextWrapping = TextWrapping.Wrap
        };
        var validate = new Button { Content = "Validate bundled character / 기본 캐릭터 검사", HorizontalAlignment = HorizontalAlignment.Left };
        validate.Click += async (_, _) => await ValidateBundledCharacterAsync();
        var panel = FormPanel();
        panel.Children.Add(info);
        panel.Children.Add(validate);
        panel.Children.Add(_validation);
        return panel;
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
        _scale.Value = (decimal)_settings.CharacterScale;
        _topmost.IsChecked = _settings.AlwaysOnTop;
        _sound.IsChecked = _settings.SoundEnabled;
        _dnd.IsChecked = _settings.DoNotDisturb;
        _dirty = false;
        await RefreshStatusAsync();
    }

    private async Task SaveAsync()
    {
        _settings = _settings with
        {
            Language = _language.SelectedIndex switch { 1 => "en", 2 => "ko", _ => "auto" },
            MovementSpeed = Enum.TryParse<MovementSpeed>(_speed.SelectedItem?.ToString(), out var speed) ? speed : MovementSpeed.Slow,
            CharacterScale = (double)(_scale.Value ?? 2),
            AlwaysOnTop = _topmost.IsChecked == true,
            SoundEnabled = _sound.IsChecked == true,
            DoNotDisturb = _dnd.IsChecked == true
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
        var candidate = Path.Combine(AppContext.BaseDirectory, "characters", "DefaultCat");
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
