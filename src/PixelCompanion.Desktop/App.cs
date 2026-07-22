using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using PixelCompanion.Core.Models;
using PixelCompanion.Core.Services;

namespace PixelCompanion.Desktop;

public sealed class App : Application
{
    private TrayIcon? _trayIcon;
    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
        RequestedThemeVariant = ThemeVariant.Light;
    }

    public override async void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            var paths = new AppPaths();
            paths.EnsureCreated();
            var store = new AtomicJsonStore();
            var settings = await store.LoadOrCreateAsync(paths.SettingsFile, () => new AppSettings());
            var state = await store.LoadOrCreateAsync(paths.PetStateFile, () => new PetState());
            state = new PetStateService().ApplyElapsed(state, DateTimeOffset.UtcNow);

            var language = LocalizationService.ResolveInitialLanguage(settings.Language);
            var basePath = Path.Combine(AppContext.BaseDirectory, "locales");
            var english = await LocalizationService.LoadFileAsync(Path.Combine(basePath, "en.json"));
            var selected = language == "en" ? english : await LocalizationService.LoadFileAsync(Path.Combine(basePath, language + ".json"));
            var localization = new LocalizationService(english, selected, key => LogMissingKey(paths, key));
            localization.ChangeLanguage(language, selected);

            var window = new PetWindow(desktop, paths, store, localization, settings, state);
            desktop.MainWindow = window;
            InstallTrayIcon(desktop, window, localization);
            if (settings.CharacterVisible) window.Show();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void InstallTrayIcon(IClassicDesktopStyleApplicationLifetime desktop, PetWindow window, LocalizationService localization)
    {
        var menu = new NativeMenu();
        menu.Items.Add(TrayItem("menu.show", () => { if (!window.IsVisible) window.Show(); window.Activate(); }, "Show character"));
        menu.Items.Add(TrayItem("menu.hide", window.Hide, "Hide character"));
        menu.Items.Add(new NativeMenuItemSeparator());
        menu.Items.Add(TrayItem(window.BehaviorPaused ? "menu.resume" : "menu.pause", window.TogglePause, "Pause behavior"));
        menu.Items.Add(TrayItem("menu.advancedSettings", window.OpenAdvancedSettings, "Advanced settings"));
        menu.Items.Add(new NativeMenuItemSeparator());
        menu.Items.Add(TrayItem("menu.exit", () => desktop.Shutdown(), "Exit"));

        _trayIcon = new TrayIcon
        {
            Icon = new WindowIcon(AssetLoader.Open(new Uri("avares://PixelCompanion/Assets/default-cat.png"))),
            ToolTipText = localization.Get("app.name"),
            Menu = menu,
            IsVisible = true
        };
        _trayIcon.Clicked += (_, _) => { if (!window.IsVisible) window.Show(); window.Activate(); };
        TrayIcon.SetIcons(this, new TrayIcons { _trayIcon });
        desktop.Exit += (_, _) => _trayIcon?.Dispose();

        NativeMenuItem TrayItem(string key, Action action, string fallback)
        {
            var header = localization.Get(key);
            if (header == key) header = fallback;
            var item = new NativeMenuItem(header);
            item.Click += (_, _) => action();
            return item;
        }
    }

    private static void LogMissingKey(AppPaths paths, string key)
    {
        try { File.AppendAllText(Path.Combine(paths.Logs, "localization.log"), $"{DateTimeOffset.Now:O} missing:{key}{Environment.NewLine}"); }
        catch (IOException) { }
    }
}
