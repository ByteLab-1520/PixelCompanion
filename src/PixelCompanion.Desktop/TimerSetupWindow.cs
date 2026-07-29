using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using PixelCompanion.Core.Models;
using PixelCompanion.Core.Services;

namespace PixelCompanion.Desktop;

public sealed record TimerRequest(AssistantTimerKind Kind, int Minutes);

public sealed class TimerSetupWindow : Window
{
    private sealed record KindOption(AssistantTimerKind Kind, string Label)
    {
        public override string ToString() => Label;
    }

    public TimerSetupWindow(LocalizationService localization)
    {
        Title = localization.Get("timer.custom");
        Width = 380;
        Height = 260;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var kind = new ComboBox
        {
            ItemsSource = new[]
            {
                new KindOption(AssistantTimerKind.General, localization.Get("timer.general")),
                new KindOption(AssistantTimerKind.Focus, localization.Get("timer.focus")),
                new KindOption(AssistantTimerKind.Rest, localization.Get("timer.rest"))
            },
            SelectedIndex = 0
        };
        var minutes = new NumericUpDown
        {
            Minimum = 1,
            Maximum = 1440,
            Value = 15,
            Increment = 1
        };
        var start = new Button { Content = localization.Get("timer.start"), MinWidth = 90 };
        var cancel = new Button { Content = localization.Get("common.cancel"), MinWidth = 90 };
        start.Click += (_, _) =>
        {
            var selected = kind.SelectedItem as KindOption;
            Close(new TimerRequest(selected?.Kind ?? AssistantTimerKind.General, (int)(minutes.Value ?? 15)));
        };
        cancel.Click += (_, _) => Close(null);

        Content = new StackPanel
        {
            Margin = new Thickness(24),
            Spacing = 14,
            Children =
            {
                new TextBlock
                {
                    Text = localization.Get("timer.customHeading"),
                    FontSize = 21,
                    FontWeight = Avalonia.Media.FontWeight.SemiBold
                },
                Field(localization.Get("timer.kind"), kind),
                Field(localization.Get("timer.minutes"), minutes),
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Spacing = 8,
                    Children = { start, cancel }
                }
            }
        };
    }

    private static Control Field(string label, Control control) => new StackPanel
    {
        Spacing = 4,
        Children =
        {
            new TextBlock { Text = label, FontWeight = Avalonia.Media.FontWeight.SemiBold },
            control
        }
    };
}
