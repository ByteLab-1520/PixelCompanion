using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using PixelCompanion.Core.Models;
using PixelCompanion.Core.Services;

namespace PixelCompanion.Desktop;

public sealed class DialogueEditorWindow : Window
{
    private sealed record Option(string Id, string Label)
    {
        public override string ToString() => Label;
    }

    private sealed class DialogueListItem(DialogueLine value) : INotifyPropertyChanged
    {
        public DialogueLine Value { get; private set; } = value;

        public string Label
        {
            get
            {
                var text = Value.Text.ReplaceLineEndings(" ").Trim();
                return text.Length > 42 ? text[..42] + "…" : text;
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public void Update(DialogueLine value)
        {
            Value = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Label)));
        }

        public override string ToString() => Label;
    }

    private readonly CharacterDialogueService _service;
    private readonly LocalizationService _localization;
    private readonly string _characterId;
    private readonly Action<string> _preview;
    private readonly ComboBox _language = new();
    private readonly ComboBox _group = new();
    private readonly ListBox _lines = new();
    private readonly TextBox _text = new()
    {
        AcceptsReturn = true,
        TextWrapping = TextWrapping.Wrap,
        MinHeight = 120,
        MaxLength = CharacterDialogueService.MaximumTextLength
    };
    private readonly NumericUpDown _probability = new()
    {
        Minimum = 0,
        Maximum = 100,
        Increment = 5,
        FormatString = "0'%'"
    };
    private readonly NumericUpDown _minimumAffection = new()
    {
        Minimum = 0,
        Maximum = 100,
        Increment = 5
    };
    private readonly NumericUpDown _cooldown = new()
    {
        Minimum = 0,
        Maximum = CharacterDialogueService.MaximumCooldownSeconds,
        Increment = 5
    };
    private readonly NumericUpDown _minimumHunger = ConditionValue();
    private readonly NumericUpDown _minimumFatigue = ConditionValue();
    private readonly NumericUpDown _maximumHappiness = ConditionValue();
    private readonly NumericUpDown _startHour = HourValue();
    private readonly NumericUpDown _endHour = HourValue();
    private readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap };
    private readonly Dictionary<string, CharacterDialogueCatalog> _catalogs = new(StringComparer.Ordinal);
    private ObservableCollection<DialogueListItem> _visibleLines = [];
    private string? _boundLanguage;
    private string? _boundGroup;
    private bool _updating;
    private bool _dirty;
    private bool _allowClose;

    public event EventHandler? DialoguesSaved;

    public DialogueEditorWindow(
        AppPaths paths,
        AtomicJsonStore store,
        LocalizationService localization,
        string characterId,
        Action<string> preview)
    {
        _service = new CharacterDialogueService(paths, store);
        _localization = localization;
        _characterId = characterId;
        _preview = preview;

        _lines.ItemTemplate = new FuncDataTemplate<DialogueListItem>(
            (_, _) => new TextBlock
            {
                [!TextBlock.TextProperty] = new Binding(nameof(DialogueListItem.Label))
            });

        Title = localization.Get("dialogueEditor.title");
        Width = 820;
        Height = 570;
        MinWidth = 700;
        MinHeight = 500;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        _language.ItemsSource = new[]
        {
            new Option("ko", "한국어 (ko)"),
            new Option("en", "English (en)")
        };
        _group.ItemsSource = new[]
        {
            new Option(DialogueGroupIds.Click, localization.Get("dialogueGroup.click")),
            new Option(DialogueGroupIds.Feed, localization.Get("dialogueGroup.feed")),
            new Option(DialogueGroupIds.Play, localization.Get("dialogueGroup.play")),
            new Option(DialogueGroupIds.Sleep, localization.Get("dialogueGroup.sleep"))
        };

        _language.SelectionChanged += (_, _) => ChangeScope();
        _group.SelectionChanged += (_, _) => ChangeScope();
        _lines.SelectionChanged += (_, _) => LoadSelectedLine();
        _text.TextChanged += (_, _) => UpdateSelectedLine();
        _probability.ValueChanged += (_, _) => UpdateSelectedLine();
        _minimumAffection.ValueChanged += (_, _) => UpdateSelectedLine();
        _cooldown.ValueChanged += (_, _) => UpdateSelectedLine();
        _minimumHunger.ValueChanged += (_, _) => UpdateSelectedLine();
        _minimumFatigue.ValueChanged += (_, _) => UpdateSelectedLine();
        _maximumHappiness.ValueChanged += (_, _) => UpdateSelectedLine();
        _startHour.ValueChanged += (_, _) => UpdateSelectedLine();
        _endHour.ValueChanged += (_, _) => UpdateSelectedLine();

        Content = BuildContent();
        Opened += async (_, _) => await LoadAsync();
        Closing += OnClosing;
    }

    private Control BuildContent()
    {
        var heading = new TextBlock
        {
            Text = _localization.Get("dialogueEditor.heading"),
            FontSize = 22,
            FontWeight = FontWeight.SemiBold
        };
        var character = new TextBlock
        {
            Text = string.Format(_localization.Get("dialogueEditor.character"), _characterId),
            Foreground = Brushes.DimGray
        };
        var selectors = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 12,
            Children =
            {
                Field(_localization.Get("dialogueEditor.language"), _language, 180),
                Field(_localization.Get("dialogueEditor.group"), _group, 220)
            }
        };

        var add = Button("dialogueEditor.add", AddLine);
        var delete = Button("dialogueEditor.delete", DeleteLine);
        var lineButtons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Children = { add, delete }
        };
        var listPanel = new Grid { RowDefinitions = new RowDefinitions("*,Auto") };
        Grid.SetRow(_lines, 0);
        Grid.SetRow(lineButtons, 1);
        listPanel.Children.Add(_lines);
        listPanel.Children.Add(lineButtons);

        var fields = new StackPanel
        {
            Spacing = 10,
            Children =
            {
                Field(_localization.Get("dialogueEditor.text"), _text),
                Field(_localization.Get("dialogueEditor.probability"), _probability),
                Field(_localization.Get("dialogueEditor.minimumAffection"), _minimumAffection),
                Field(_localization.Get("dialogueEditor.cooldown"), _cooldown),
                Field(_localization.Get("dialogueEditor.minimumHunger"), _minimumHunger),
                Field(_localization.Get("dialogueEditor.minimumFatigue"), _minimumFatigue),
                Field(_localization.Get("dialogueEditor.maximumHappiness"), _maximumHappiness),
                Field(_localization.Get("dialogueEditor.startHour"), _startHour),
                Field(_localization.Get("dialogueEditor.endHour"), _endHour),
                new TextBlock
                {
                    Text = _localization.Get("dialogueEditor.conditionHint"),
                    Foreground = Brushes.DimGray,
                    TextWrapping = TextWrapping.Wrap
                },
                new TextBlock
                {
                    Text = _localization.Get("dialogueEditor.variables"),
                    Foreground = Brushes.DimGray,
                    TextWrapping = TextWrapping.Wrap
                }
            }
        };

        var body = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("2*,3*"),
            ColumnSpacing = 18,
            ClipToBounds = true
        };
        var fieldsScroller = new ScrollViewer
        {
            Content = fields,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Padding = new Thickness(0, 0, 8, 0)
        };
        Grid.SetColumn(listPanel, 0);
        Grid.SetColumn(fieldsScroller, 1);
        body.Children.Add(listPanel);
        body.Children.Add(fieldsScroller);

        var test = Button("dialogueEditor.test", TestLine);
        var save = Button("dialogueEditor.save", async () => await SaveAsync());
        var close = Button("dialogueEditor.close", Close);
        var footerButtons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
            Children = { test, save, close }
        };
        var footer = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        Grid.SetColumn(_status, 0);
        Grid.SetColumn(footerButtons, 1);
        footer.Children.Add(_status);
        footer.Children.Add(footerButtons);

        var root = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,Auto,*,Auto"),
            RowSpacing = 14,
            Margin = new Thickness(24)
        };
        var header = new StackPanel { Spacing = 3, Children = { heading, character } };
        Grid.SetRow(header, 0);
        Grid.SetRow(selectors, 1);
        Grid.SetRow(body, 2);
        Grid.SetRow(footer, 3);
        root.Children.Add(header);
        root.Children.Add(selectors);
        root.Children.Add(body);
        root.Children.Add(footer);
        return root;
    }

    private async Task LoadAsync()
    {
        _updating = true;
        try
        {
            foreach (var language in new[] { "ko", "en" })
            {
                var resources = await LoadResourcesAsync(language);
                _catalogs[language] = await _service.LoadAsync(
                    _characterId,
                    language,
                    () => CharacterDialogueService.CreateDefaults(
                        _characterId,
                        language,
                        key => resources.GetValueOrDefault(key, key)));
            }

            _language.SelectedIndex = _localization.Language == "ko" ? 0 : 1;
            _group.SelectedIndex = 0;
            RebindLines();
            _status.Text = _localization.Get("dialogueEditor.ready");
            _dirty = false;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            _status.Text = string.Format(_localization.Get("dialogueEditor.loadFailed"), ex.Message);
        }
        finally
        {
            _updating = false;
        }
    }

    private async Task<Dictionary<string, string>> LoadResourcesAsync(string language)
    {
        var english = await LocalizationService.LoadFileAsync(
            Path.Combine(AppContext.BaseDirectory, "locales", "en.json"));
        if (language == "en") return english;
        var selected = await LocalizationService.LoadFileAsync(
            Path.Combine(AppContext.BaseDirectory, "locales", language + ".json"));
        foreach (var pair in english)
            selected.TryAdd(pair.Key, pair.Value);
        return selected;
    }

    private void ChangeScope()
    {
        if (_updating) return;
        CommitVisibleGroup();
        RebindLines();
    }

    private void RebindLines()
    {
        if (SelectedLanguage is not { } language || SelectedGroup is not { } group ||
            !_catalogs.TryGetValue(language, out var catalog))
            return;

        _updating = true;
        try
        {
            _visibleLines = new ObservableCollection<DialogueListItem>(
                catalog.GetGroup(group).Select(line => new DialogueListItem(line)));
            _boundLanguage = language;
            _boundGroup = group;
            _lines.ItemsSource = _visibleLines;
            _lines.SelectedIndex = _visibleLines.Count > 0 ? 0 : -1;
        }
        finally
        {
            _updating = false;
        }
        LoadSelectedLine();
    }

    private void CommitVisibleGroup()
    {
        if (_boundLanguage is not { } language || _boundGroup is not { } group ||
            !_catalogs.TryGetValue(language, out var catalog))
            return;
        catalog.Groups[group] = _visibleLines.Select(item => item.Value).ToList();
    }

    private void LoadSelectedLine()
    {
        if (_updating) return;
        _updating = true;
        try
        {
            if (_lines.SelectedItem is DialogueListItem item)
            {
                var line = item.Value;
                _text.Text = line.Text;
                _probability.Value = (decimal)(line.Probability * 100);
                _minimumAffection.Value = (decimal)line.MinimumAffection;
                _cooldown.Value = line.CooldownSeconds;
                _minimumHunger.Value = line.MinimumHunger is { } hunger ? (decimal)hunger : -1;
                _minimumFatigue.Value = line.MinimumFatigue is { } fatigue ? (decimal)fatigue : -1;
                _maximumHappiness.Value = line.MaximumHappiness is { } happiness ? (decimal)happiness : -1;
                _startHour.Value = line.StartHour ?? -1;
                _endHour.Value = line.EndHour ?? -1;
            }
            else
            {
                _text.Text = "";
                _probability.Value = 100;
                _minimumAffection.Value = 0;
                _cooldown.Value = 0;
                _minimumHunger.Value = -1;
                _minimumFatigue.Value = -1;
                _maximumHappiness.Value = -1;
                _startHour.Value = -1;
                _endHour.Value = -1;
            }
        }
        finally
        {
            _updating = false;
        }
    }

    private void UpdateSelectedLine()
    {
        if (_updating || _lines.SelectedItem is not DialogueListItem selected) return;
        var updated = selected.Value with
        {
            Text = _text.Text ?? "",
            Probability = (double)(_probability.Value ?? 100) / 100,
            MinimumAffection = (double)(_minimumAffection.Value ?? 0),
            CooldownSeconds = (int)(_cooldown.Value ?? 0),
            MinimumHunger = OptionalDouble(_minimumHunger.Value),
            MinimumFatigue = OptionalDouble(_minimumFatigue.Value),
            MaximumHappiness = OptionalDouble(_maximumHappiness.Value),
            StartHour = OptionalInt(_startHour.Value),
            EndHour = OptionalInt(_endHour.Value)
        };
        selected.Update(updated);
        _dirty = true;
    }

    private void AddLine()
    {
        if (_visibleLines.Count >= CharacterDialogueService.MaximumLinesPerGroup)
        {
            _status.Text = _localization.Get("dialogueEditor.tooMany");
            return;
        }
        var group = SelectedGroup ?? DialogueGroupIds.Click;
        var line = new DialogueLine(
            $"{group}.{Guid.NewGuid():N}",
            _localization.Get("dialogueEditor.newLine"));
        var item = new DialogueListItem(line);
        _visibleLines.Add(item);
        _lines.SelectedItem = item;
        _dirty = true;
    }

    private void DeleteLine()
    {
        if (_lines.SelectedItem is not DialogueListItem selected) return;
        var index = _visibleLines.IndexOf(selected);
        _visibleLines.Remove(selected);
        _lines.SelectedIndex = _visibleLines.Count == 0 ? -1 : Math.Min(index, _visibleLines.Count - 1);
        _dirty = true;
    }

    private void TestLine()
    {
        if (_lines.SelectedItem is DialogueListItem { Value.Text.Length: > 0 } item)
            _preview(item.Value.Text);
    }

    private async Task SaveAsync()
    {
        CommitVisibleGroup();
        try
        {
            foreach (var catalog in _catalogs.Values)
                await _service.SaveAsync(catalog);
            _dirty = false;
            _status.Text = _localization.Get("dialogueEditor.saved");
            DialoguesSaved?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            _status.Text = string.Format(_localization.Get("dialogueEditor.saveFailed"), ex.Message);
        }
    }

    private async void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (!_dirty || _allowClose) return;
        e.Cancel = true;
        var discard = await ShowDiscardConfirmationAsync();
        if (!discard) return;
        _allowClose = true;
        Close();
    }

    private async Task<bool> ShowDiscardConfirmationAsync()
    {
        var dialog = new Window
        {
            Title = _localization.Get("dialogueEditor.unsavedTitle"),
            Width = 420,
            Height = 180,
            CanResize = false,
            Topmost = true,
            ShowActivated = true,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        var discard = new Button { Content = _localization.Get("dialogueEditor.discard"), MinWidth = 100 };
        var cancel = new Button { Content = _localization.Get("dialogueEditor.cancel"), MinWidth = 100 };
        discard.Click += (_, _) => dialog.Close(true);
        cancel.Click += (_, _) => dialog.Close(false);
        dialog.Opened += (_, _) =>
        {
            dialog.Activate();
            cancel.Focus();
        };
        dialog.Content = new StackPanel
        {
            Margin = new Thickness(22),
            Spacing = 18,
            Children =
            {
                new TextBlock
                {
                    Text = _localization.Get("dialogueEditor.unsaved"),
                    TextWrapping = TextWrapping.Wrap
                },
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Spacing = 8,
                    Children = { discard, cancel }
                }
            }
        };
        return await dialog.ShowDialog<bool>(this);
    }

    private string? SelectedLanguage => (_language.SelectedItem as Option)?.Id;
    private string? SelectedGroup => (_group.SelectedItem as Option)?.Id;

    private Button Button(string key, Action action)
    {
        var button = new Button { Content = _localization.Get(key), MinWidth = 90 };
        button.Click += (_, _) => action();
        return button;
    }

    private static Control Field(string label, Control control, double? width = null)
    {
        if (width is { } value) control.Width = value;
        return new StackPanel
        {
            Spacing = 4,
            Children =
            {
                new TextBlock { Text = label, FontWeight = FontWeight.SemiBold },
                control
            }
        };
    }

    private static NumericUpDown ConditionValue() => new()
    {
        Minimum = -1,
        Maximum = 100,
        Increment = 5,
        Value = -1
    };

    private static NumericUpDown HourValue() => new()
    {
        Minimum = -1,
        Maximum = 23,
        Increment = 1,
        Value = -1
    };

    private static double? OptionalDouble(decimal? value) =>
        value is { } number && number >= 0 ? (double)number : null;

    private static int? OptionalInt(decimal? value) =>
        value is { } number && number >= 0 ? (int)number : null;
}
