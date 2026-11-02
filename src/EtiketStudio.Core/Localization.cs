// SPDX-License-Identifier: GPL-3.0-only
using System.Globalization;
using System.Resources;

namespace EtiketStudio.Core;

public static class T
{
    public static LanguageCatalog Catalog { get; } = new();
    public static string Get(string key, params object[] arguments) => Catalog.Get(key, arguments);
}

/// <summary>The saved app language is authoritative even on threads carrying the Windows UI culture.</summary>
public sealed class LanguageCatalog(string language = "en-US") : ObservableObject
{
    public const string PreferenceKey = "preferredUiLanguage";
    public static string StartupLanguage(Store store) => Normalize(store.Load(PreferenceKey, () => "en-US"));

    // Both catalogs are in the main assembly: single-file publishing needs no satellite resource file.
    private static readonly ResourceManager Turkish = new("EtiketStudio.Core.Resources.Strings", typeof(T).Assembly);
    private static readonly ResourceManager English = new("EtiketStudio.Core.Resources.StringsEnglish", typeof(T).Assembly);
    private CultureInfo culture = CultureInfo.GetCultureInfo(Normalize(language));
    public string Language => culture.Name;
    public int Revision { get; private set; }
    public string this[string key] => Get(key);
    public static string Normalize(string? code) => code is not null && (code.Equals("en-US", StringComparison.OrdinalIgnoreCase) || code.Equals("en", StringComparison.OrdinalIgnoreCase)) ? "en-US" : "tr-TR";
    public void Apply(string language)
    {
        var next = CultureInfo.GetCultureInfo(Normalize(language));
        if (next.Name == culture.Name) return;
        culture = next;
        Revision++;
        Notify(nameof(Language)); Notify(nameof(Revision)); Notify("Item[]");
    }
    public string Get(string key, params object[] arguments)
    {
        var selectedCulture = culture;
        var resources = selectedCulture.Name == "en-US" ? English : Turkish;
        string value = resources.GetString(key, CultureInfo.InvariantCulture) ?? Turkish.GetString(key, CultureInfo.InvariantCulture) ?? key;
        return arguments.Length == 0 ? value : string.Format(selectedCulture, value, arguments);
    }
}
public sealed class DomainException(string key, params object[] arguments) : Exception(T.Get(key, arguments))
{
    public string Key { get; } = key;
}
