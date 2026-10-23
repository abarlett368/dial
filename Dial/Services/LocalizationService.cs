using System.Collections;
using System.Globalization;
using System.Resources;
using System.Windows;

namespace Dial.Services;

public static class LocalizationService
{
    private const string BaseName = "Dial.Resources.Resources";
    private const string MarkerKey = "Dial.Localization";

    private static readonly ResourceManager Manager = new(BaseName, typeof(LocalizationService).Assembly);

    public static event Action? LanguageChanged;

    public static CultureInfo CurrentCulture { get; private set; } = CultureInfo.GetCultureInfo("en");

    public static string T(string key) => Manager.GetString(key, CurrentCulture) ?? key;

    public static void SetLanguage(string cultureCode)
    {
        if (cultureCode is not ("en" or "zh-CN"))
            throw new CultureNotFoundException(nameof(cultureCode), cultureCode, "The language is not supported.");
        CurrentCulture = CultureInfo.GetCultureInfo(cultureCode);

        CultureInfo.CurrentUICulture = CurrentCulture;
        CultureInfo.CurrentCulture = CurrentCulture;

        var app = Application.Current;
        if (app is not null)
        {
            var dictionaries = app.Resources.MergedDictionaries;
            var existing = dictionaries.FirstOrDefault(d => d.Contains(MarkerKey));
            if (existing is not null) dictionaries.Remove(existing);
            dictionaries.Add(BuildResourceDictionary());
        }

        LanguageChanged?.Invoke();
    }

    public static ResourceDictionary BuildResourceDictionary()
    {
        var dict = new ResourceDictionary { [MarkerKey] = true };

        var set = Manager.GetResourceSet(CurrentCulture, true, true);
        if (set is null) return dict;

        foreach (DictionaryEntry entry in set)
        {
            if (entry.Key is string key && entry.Value is string value)
            {
                dict[key] = value;
            }
        }

        return dict;
    }
}
