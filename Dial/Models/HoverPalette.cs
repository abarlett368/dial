using System.Windows.Media;

namespace Dial.Models;

public sealed record HoverColorOption(string Key, string LocKey, string LightHex, string DarkHex);

public static class HoverPalette
{
    public static readonly HoverColorOption[] Options =
    {
        new("windowsBlue", "AccentWindowsBlue", "#357EC7", "#0F6796"),
        new("systemAccent", "AccentSystem", "", ""),
        new("seaBlue", "AccentSeaBlue", "#43A2E6", "#1167A7"),
        new("teal", "AccentTeal", "#11AEA2", "#1B6961"),
        new("cobalt", "AccentCobalt", "#7F9FD8", "#175CE7"),
        new("indigo", "AccentIndigo", "#929DD2", "#3C55EC"),
        new("violet", "AccentViolet", "#B092D2", "#8440D8"),
        new("pinkPurple", "AccentPinkPurple", "#C389C2", "#AD2AA2"),
        new("magenta", "AccentMagenta", "#D882B4", "#C21371"),
        new("red", "AccentRed", "#D5898D", "#C81C14"),
        new("orange", "AccentOrange", "#D78853", "#93500F"),
        new("gold", "AccentGold", "#BD9713", "#715B10"),
        new("green", "AccentGreen", "#2EB15A", "#236D2F"),
        new("graphite", "AccentGraphite", "#909DAD", "#516781"),
        new("black", "AccentBlack", "#9C9C9C", "#656565"),
    };

    public static string Resolve(string? key, bool dark)
    {
        if (key == "systemAccent")
        {
            var color = AdaptSystemAccent(Services.ThemeService.WindowsAccent, dark);
            return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
        }
        var option = Options.FirstOrDefault(o => o.Key == key) ?? Options[0];
        return dark ? option.DarkHex : option.LightHex;
    }

    public static bool IsValid(string? key) => Options.Any(o => o.Key == key);

    private static Color AdaptSystemAccent(Windows.UI.Color accent, bool dark)
    {
        var original = Color.FromRgb(accent.R, accent.G, accent.B);
        byte endpoint = dark ? (byte)0 : (byte)255;
        Color Mix(double amount)
        {
            byte Channel(byte value) => (byte)Math.Round(value + (endpoint - value) * amount);
            return Color.FromRgb(Channel(original.R), Channel(original.G), Channel(original.B));
        }
        bool Readable(Color color)
        {
            double alpha = (dark ? 233 : 221) / 255.0;
            byte Channel(byte value) => (byte)Math.Round(value * alpha + (dark ? 255 : 0) * (1 - alpha));
            double background = Luminance(Color.FromRgb(Channel(color.R), Channel(color.G), Channel(color.B)));
            double foreground = dark ? 1 : Luminance(Color.FromRgb(26, 26, 26));
            return (Math.Max(background, foreground) + 0.05) / (Math.Min(background, foreground) + 0.05) >= 4.8;
        }
        if (Readable(original)) return original;
        double lower = 0, upper = 1;
        for (int iteration = 0; iteration < 16; iteration++)
        {
            double middle = (lower + upper) / 2;
            if (Readable(Mix(middle))) upper = middle;
            else lower = middle;
        }
        return Mix(upper);
    }

    internal static double Luminance(Color color)
    {
        static double Linear(byte channel)
        {
            double value = channel / 255.0;
            return value <= 0.04045 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
        }
        return 0.2126 * Linear(color.R) + 0.7152 * Linear(color.G) + 0.0722 * Linear(color.B);
    }
}
