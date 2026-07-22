using System.Globalization;
using System.Text.Json;

namespace PixelCompanion.Core.Services;

public sealed class LocalizationService
{
    private readonly IReadOnlyDictionary<string, string> _english;
    private IReadOnlyDictionary<string, string> _current;
    private readonly Action<string>? _missingKeyLogger;

    public LocalizationService(IReadOnlyDictionary<string, string> english, IReadOnlyDictionary<string, string>? selected = null, Action<string>? missingKeyLogger = null)
    {
        _english = english;
        _current = selected ?? english;
        _missingKeyLogger = missingKeyLogger;
    }

    public string Language { get; private set; } = "en";
    public event EventHandler? LanguageChanged;

    public string Get(string key, IReadOnlyDictionary<string, object?>? variables = null)
    {
        if (!_current.TryGetValue(key, out var value) && !_english.TryGetValue(key, out value))
        {
            _missingKeyLogger?.Invoke(key);
            return key;
        }

        if (variables is null) return value;
        foreach (var pair in variables)
            value = value.Replace("{" + pair.Key + "}", Convert.ToString(pair.Value, CultureInfo.CurrentCulture), StringComparison.Ordinal);
        return value;
    }

    public void ChangeLanguage(string language, IReadOnlyDictionary<string, string>? resources)
    {
        Language = language;
        _current = resources ?? _english;
        LanguageChanged?.Invoke(this, EventArgs.Empty);
    }

    public static async Task<Dictionary<string, string>> LoadFileAsync(string path, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(path)) return [];
        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<Dictionary<string, string>>(stream, cancellationToken: cancellationToken) ?? [];
    }

    public static string ResolveInitialLanguage(string configuredLanguage)
    {
        if (configuredLanguage is "ko" or "en") return configuredLanguage;
        return CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ko" ? "ko" : "en";
    }
}

