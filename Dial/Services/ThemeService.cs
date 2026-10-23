using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;

namespace Dial.Services;

public static class ThemeService
{
    private const string MarkerKey = "Dial.Theme";
    private static readonly Windows.UI.ViewManagement.UISettings WindowsSettings = new();
    private static Windows.UI.Color _lastWindowsAccent = WindowsAccent;
    public static Windows.UI.Color WindowsAccent =>
        WindowsSettings.GetColorValue(Windows.UI.ViewManagement.UIColorType.Accent);

    public static string CurrentTheme { get; private set; } = "Light";

    public static bool IsDark => CurrentTheme != "Light";

    public static event Action? ThemeChanged;

    static ThemeService()
    {
        WindowsSettings.ColorValuesChanged += OnWindowsColorsChanged;
        SystemEvents.UserPreferenceChanged += (_, _) =>
        {
            if (Application.Current is { } app && !app.Dispatcher.HasShutdownStarted)
                app.Dispatcher.BeginInvoke(() => SetTheme(CurrentTheme));
        };
    }

    private static void OnWindowsColorsChanged(Windows.UI.ViewManagement.UISettings sender, object args)
    {
        if (Application.Current is { } app && !app.Dispatcher.HasShutdownStarted)
            app.Dispatcher.BeginInvoke(() =>
            {
                var accent = WindowsAccent;
                if (accent.Equals(_lastWindowsAccent)) return;
                _lastWindowsAccent = accent;
                SetTheme(CurrentTheme);
            });
    }

    public static void SetTheme(string theme)
    {
        CurrentTheme = string.Equals(theme, "Light", StringComparison.OrdinalIgnoreCase) ? "Light" : "Dark";

        if (Application.Current is { } app)
        {
            var dictionaries = app.Resources.MergedDictionaries;
            var existing = dictionaries.FirstOrDefault(d => d.Contains(MarkerKey));
            if (existing is not null) dictionaries.Remove(existing);
            dictionaries.Add(BuildDictionary());
        }

        ThemeChanged?.Invoke();
    }

    public static SolidColorBrush NewBrush(string hex, byte alpha = 255)
    {
        var color = (Color)ColorConverter.ConvertFromString(hex);
        return new SolidColorBrush(Color.FromArgb(alpha, color.R, color.G, color.B));
    }

    private static ResourceDictionary BuildDictionary()
    {
        var d = new ResourceDictionary { [MarkerKey] = true };
        d["SuccessTextBrush"] = Freeze(IsDark ? "#6CCB5F" : "#0F6C0F");
        d["SelectionBackgroundBrush"] = Freeze(IsDark ? "#454545" : "#E5E5E5");
        d["SelectionForegroundBrush"] = Freeze(IsDark ? "#FFFFFF" : "#1A1A1A");

        if (IsDark)
        {
            d["AppBackgroundBrush"] = Freeze("#242424");
            d["AppForegroundBrush"] = Freeze("#FFFFFF");
            d["SecondaryTextBrush"] = Freeze("#B0B0B0");
            d["FieldLabelBrush"] = Freeze("#D0D0D0");
            d["ControlBackgroundBrush"] = Freeze("#333333");
            d["ControlBorderBrush"] = Freeze("#484848");
            d["ControlHoverBrush"] = Freeze("#404040");
            d["PanelBackgroundBrush"] = Freeze("#2E2E2E");
            d["InputBackgroundBrush"] = Freeze("#242424");
            d["AccentBrush"] = Freeze("#4CC2FF");
            d["AccentHoverBrush"] = Freeze("#3AB6F4");
        }
        else
        {
            d["AppBackgroundBrush"] = Freeze("#F8F8F8");
            d["AppForegroundBrush"] = Freeze("#1A1A1A");
            d["SecondaryTextBrush"] = Freeze("#5F5F5F");
            d["FieldLabelBrush"] = Freeze("#505050");
            d["ControlBackgroundBrush"] = Freeze("#FFFFFF");
            d["ControlBorderBrush"] = Freeze("#D0D0D0");
            d["ControlHoverBrush"] = Freeze("#ECECEC");
            d["PanelBackgroundBrush"] = Freeze("#F1F1F1");
            d["InputBackgroundBrush"] = Freeze("#FFFFFF");
            d["AccentBrush"] = Freeze("#357EC7");
            d["AccentHoverBrush"] = Freeze("#4A91D0");
        }

        if (SystemParameters.HighContrast)
        {
            foreach (string key in new[] { "AppBackgroundBrush", "ControlBackgroundBrush", "PanelBackgroundBrush", "InputBackgroundBrush" })
                d[key] = SystemColors.WindowBrush;
            foreach (string key in new[] { "AppForegroundBrush", "SecondaryTextBrush", "FieldLabelBrush", "ControlBorderBrush", "SuccessTextBrush" })
                d[key] = SystemColors.WindowTextBrush;
            d["AccentBrush"] = SystemColors.HighlightBrush;
            d["AccentHoverBrush"] = SystemColors.HighlightBrush;
            d["ControlHoverBrush"] = SystemColors.ControlBrush;
            d["SelectionBackgroundBrush"] = SystemColors.HighlightBrush;
            d["SelectionForegroundBrush"] = SystemColors.HighlightTextBrush;
        }
        d["AccentForegroundBrush"] = SystemParameters.HighContrast ? SystemColors.HighlightTextBrush
            : Brushes.Black;
        return d;
    }

    private static SolidColorBrush Freeze(string hex)
    {
        var brush = NewBrush(hex);
        brush.Freeze();
        return brush;
    }
}
