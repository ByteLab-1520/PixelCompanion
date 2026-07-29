using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform;
using PixelCompanion.Core.Models;
using PixelCompanion.Core.Services;

namespace PixelCompanion.Desktop;

public sealed class MovementRegionEditorWindow : Window
{
    private enum EditMode { None, Draw, Move, Resize }

    private sealed class EditableRegion
    {
        public required string Id { get; init; }
        public required string Name { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
    }

    private readonly AppPaths _paths;
    private readonly AtomicJsonStore _store;
    private readonly LocalizationService _localization;
    private readonly Canvas _canvas = new();
    private readonly TextBox _name = new() { MinWidth = 150 };
    private readonly List<EditableRegion> _regions = [];
    private EditableRegion? _selected;
    private EditMode _editMode;
    private Point _pointerStart;
    private Rect _regionStart;
    private double _coordinateScale = 1;
    private PixelPoint _virtualOrigin;

    public MovementRegionEditorWindow(AppPaths paths, AtomicJsonStore store, LocalizationService localization)
    {
        _paths = paths;
        _store = store;
        _localization = localization;
        Title = localization.Get("regionEditor.title");
        WindowDecorations = WindowDecorations.None;
        TransparencyLevelHint = [WindowTransparencyLevel.Transparent];
        Background = new SolidColorBrush(Color.FromArgb(105, 15, 20, 26));
        Topmost = true;
        ShowInTaskbar = false;
        CanResize = false;

        _canvas.Background = Brushes.Transparent;
        _canvas.PointerPressed += CanvasPointerPressed;
        _canvas.PointerMoved += CanvasPointerMoved;
        _canvas.PointerReleased += CanvasPointerReleased;

        _name.PlaceholderText = localization.Get("regionEditor.name");
        _name.LostFocus += (_, _) =>
        {
            if (_selected is null || string.IsNullOrWhiteSpace(_name.Text)) return;
            _selected.Name = _name.Text.Trim();
            RenderRegions();
        };

        var save = Button("regionEditor.save", async () => await SaveAsync());
        var cancel = Button("regionEditor.cancel", Close);
        var delete = Button("regionEditor.delete", DeleteSelected);
        var clear = Button("regionEditor.clear", () =>
        {
            _regions.Clear();
            Select(null);
            RenderRegions();
        });
        var currentMonitor = Button("regionEditor.currentMonitor", AddPrimaryWorkingArea);
        var allMonitors = Button("regionEditor.allMonitors", AddAllWorkingAreas);

        var toolbar = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(245, 248, 248, 248)),
            BorderBrush = Brushes.DimGray,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(12),
            Margin = new Thickness(16),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Child = new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    new TextBlock
                    {
                        Text = localization.Get("regionEditor.help"),
                        FontWeight = FontWeight.SemiBold,
                        MaxWidth = 560,
                        TextWrapping = TextWrapping.Wrap
                    },
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Spacing = 8,
                        Children = { currentMonitor, allMonitors, delete, clear }
                    },
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Spacing = 8,
                        Children =
                        {
                            new TextBlock
                            {
                                Text = localization.Get("regionEditor.name"),
                                VerticalAlignment = VerticalAlignment.Center
                            },
                            _name,
                            save,
                            cancel
                        }
                    }
                }
            }
        };

        var root = new Grid();
        root.Children.Add(_canvas);
        root.Children.Add(toolbar);
        Content = root;

        Opened += async (_, _) => await InitializeAsync();
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) Close();
            if (e.Key == Key.Delete) DeleteSelected();
        };
    }

    private async Task InitializeAsync()
    {
        var screens = Screens.All;
        if (screens.Count == 0)
        {
            Close();
            return;
        }

        var left = screens.Min(screen => screen.Bounds.X);
        var top = screens.Min(screen => screen.Bounds.Y);
        var right = screens.Max(screen => screen.Bounds.Right);
        var bottom = screens.Max(screen => screen.Bounds.Bottom);
        _virtualOrigin = new PixelPoint(left, top);
        _coordinateScale = Screens.ScreenFromPoint(_virtualOrigin)?.Scaling ?? Screens.Primary?.Scaling ?? 1;
        Position = _virtualOrigin;
        Width = (right - left) / _coordinateScale;
        Height = (bottom - top) / _coordinateScale;

        var collection = await _store.LoadOrCreateAsync(_paths.RegionsFile, () => new MovementRegionCollection());
        foreach (var region in collection.Regions.Where(region => region.IsValid))
        {
            _regions.Add(new EditableRegion
            {
                Id = region.Id,
                Name = region.Name,
                X = (region.X - left) / _coordinateScale,
                Y = (region.Y - top) / _coordinateScale,
                Width = region.Width / _coordinateScale,
                Height = region.Height / _coordinateScale
            });
        }
        RenderRegions();
    }

    private void CanvasPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var point = e.GetCurrentPoint(_canvas);
        if (!point.Properties.IsLeftButtonPressed) return;
        _pointerStart = point.Position;
        _selected = HitTest(_pointerStart);
        if (_selected is null)
        {
            _selected = new EditableRegion
            {
                Id = Guid.NewGuid().ToString("N"),
                Name = string.Format(_localization.Get("regionEditor.defaultName"), _regions.Count + 1),
                X = _pointerStart.X,
                Y = _pointerStart.Y,
                Width = 1,
                Height = 1
            };
            _regions.Add(_selected);
            _editMode = EditMode.Draw;
        }
        else
        {
            _regionStart = new Rect(_selected.X, _selected.Y, _selected.Width, _selected.Height);
            _editMode = IsResizeHandle(_selected, _pointerStart) ? EditMode.Resize : EditMode.Move;
        }
        Select(_selected);
        e.Pointer.Capture(_canvas);
        e.Handled = true;
    }

    private void CanvasPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_selected is null || _editMode == EditMode.None) return;
        var point = e.GetPosition(_canvas);
        var dx = point.X - _pointerStart.X;
        var dy = point.Y - _pointerStart.Y;
        switch (_editMode)
        {
            case EditMode.Draw:
                _selected.X = Math.Min(_pointerStart.X, point.X);
                _selected.Y = Math.Min(_pointerStart.Y, point.Y);
                _selected.Width = Math.Abs(dx);
                _selected.Height = Math.Abs(dy);
                break;
            case EditMode.Move:
                _selected.X = Math.Clamp(_regionStart.X + dx, 0, Math.Max(0, _canvas.Bounds.Width - _regionStart.Width));
                _selected.Y = Math.Clamp(_regionStart.Y + dy, 0, Math.Max(0, _canvas.Bounds.Height - _regionStart.Height));
                break;
            case EditMode.Resize:
                _selected.Width = Math.Max(24, _regionStart.Width + dx);
                _selected.Height = Math.Max(24, _regionStart.Height + dy);
                break;
        }
        RenderRegions();
    }

    private void CanvasPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_selected is not null && (_selected.Width < 20 || _selected.Height < 20))
        {
            _regions.Remove(_selected);
            Select(null);
        }
        _editMode = EditMode.None;
        e.Pointer.Capture(null);
        RenderRegions();
    }

    private EditableRegion? HitTest(Point point) =>
        _regions.LastOrDefault(region =>
            point.X >= region.X && point.X <= region.X + region.Width &&
            point.Y >= region.Y && point.Y <= region.Y + region.Height);

    private static bool IsResizeHandle(EditableRegion region, Point point) =>
        point.X >= region.X + region.Width - 18 && point.Y >= region.Y + region.Height - 18;

    private void Select(EditableRegion? region)
    {
        _selected = region;
        _name.Text = region?.Name ?? "";
    }

    private void DeleteSelected()
    {
        if (_selected is null) return;
        _regions.Remove(_selected);
        Select(null);
        RenderRegions();
    }

    private void AddPrimaryWorkingArea()
    {
        if (Screens.Primary is { } screen) AddWorkingArea(screen);
    }

    private void AddAllWorkingAreas()
    {
        foreach (var screen in Screens.All) AddWorkingArea(screen);
    }

    private void AddWorkingArea(Screen screen)
    {
        var area = screen.WorkingArea;
        var region = new EditableRegion
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = string.Format(_localization.Get("regionEditor.defaultName"), _regions.Count + 1),
            X = (area.X - _virtualOrigin.X) / _coordinateScale,
            Y = (area.Y - _virtualOrigin.Y) / _coordinateScale,
            Width = area.Width / _coordinateScale,
            Height = area.Height / _coordinateScale
        };
        _regions.Add(region);
        Select(region);
        RenderRegions();
    }

    private void RenderRegions()
    {
        _canvas.Children.Clear();
        foreach (var region in _regions)
        {
            var selected = region == _selected;
            var border = new Border
            {
                Width = region.Width,
                Height = region.Height,
                Background = new SolidColorBrush(Color.FromArgb(selected ? (byte)100 : (byte)68, 75, 170, 255)),
                BorderBrush = selected ? Brushes.White : Brushes.DeepSkyBlue,
                BorderThickness = new Thickness(selected ? 3 : 2),
                Child = new TextBlock
                {
                    Text = region.Name,
                    Foreground = Brushes.White,
                    FontWeight = FontWeight.SemiBold,
                    Margin = new Thickness(8),
                    IsHitTestVisible = false
                },
                IsHitTestVisible = false
            };
            Canvas.SetLeft(border, region.X);
            Canvas.SetTop(border, region.Y);
            _canvas.Children.Add(border);

            if (!selected) continue;
            var handle = new Border
            {
                Width = 16,
                Height = 16,
                Background = Brushes.White,
                BorderBrush = Brushes.DodgerBlue,
                BorderThickness = new Thickness(2),
                IsHitTestVisible = false
            };
            Canvas.SetLeft(handle, region.X + region.Width - 12);
            Canvas.SetTop(handle, region.Y + region.Height - 12);
            _canvas.Children.Add(handle);
        }
    }

    private async Task SaveAsync()
    {
        var collection = new MovementRegionCollection
        {
            Regions = _regions
                .Where(region => region.Width >= 20 && region.Height >= 20)
                .Select(region => new MovementRegion(
                    region.Id,
                    region.Name,
                    _virtualOrigin.X + (region.X * _coordinateScale),
                    _virtualOrigin.Y + (region.Y * _coordinateScale),
                    region.Width * _coordinateScale,
                    region.Height * _coordinateScale))
                .ToList()
        };
        await _store.SaveAsync(_paths.RegionsFile, collection);
        var settings = (await _store.LoadOrCreateAsync(
            _paths.SettingsFile,
            () => new AppSettings())).Migrate();
        await _store.SaveAsync(_paths.SettingsFile, settings with { RegionMode = RegionMode.Custom });
        Close();
    }

    private Button Button(string key, Action action)
    {
        var button = new Button { Content = _localization.Get(key), MinHeight = 30 };
        button.Click += (_, _) => action();
        return button;
    }
}
