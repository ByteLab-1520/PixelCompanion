using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using PixelCompanion.Core.Models;
using PixelCompanion.Core.Services;

namespace PixelCompanion.Desktop;

public sealed class PetStatusWindow : Window
{
    public PetStatusWindow(PetState state, LocalizationService localization)
    {
        Title = localization.Get("status.title");
        Width = 430;
        Height = 410;
        MinWidth = 380;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var close = new Button
        {
            Content = localization.Get("common.close"),
            MinWidth = 90,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        close.Click += (_, _) => Close();

        Content = new StackPanel
        {
            Margin = new Thickness(24),
            Spacing = 14,
            Children =
            {
                new TextBlock
                {
                    Text = localization.Get("status.heading"),
                    FontSize = 23,
                    FontWeight = FontWeight.SemiBold
                },
                Meter(localization.Get("status.hunger"), state.Hunger, higherIsBetter: false),
                Meter(localization.Get("status.cleanliness"), state.Cleanliness, higherIsBetter: true),
                Meter(localization.Get("status.happiness"), state.Happiness, higherIsBetter: true),
                Meter(localization.Get("status.fatigue"), state.Fatigue, higherIsBetter: false),
                Meter(localization.Get("status.affection"), state.Affection, higherIsBetter: true),
                new TextBlock
                {
                    Text = string.Format(localization.Get("status.mood"), localization.Get($"mood.{state.Mood.ToString().ToLowerInvariant()}")),
                    Foreground = Brushes.DimGray
                },
                close
            }
        };
    }

    private static Control Meter(string label, double value, bool higherIsBetter)
    {
        var safe = Math.Clamp(value, 0, 100);
        var good = higherIsBetter ? safe >= 55 : safe <= 45;
        return new StackPanel
        {
            Spacing = 4,
            Children =
            {
                new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                    Children =
                    {
                        new TextBlock { Text = label, FontWeight = FontWeight.SemiBold },
                        ValueText()
                    }
                },
                new ProgressBar
                {
                    Minimum = 0,
                    Maximum = 100,
                    Value = safe,
                    Height = 12,
                    Foreground = good ? Brushes.MediumSeaGreen : Brushes.DarkOrange
                }
            }
        };

        TextBlock ValueText()
        {
            var text = new TextBlock { Text = $"{safe:0} / 100" };
            Grid.SetColumn(text, 1);
            return text;
        }
    }
}
