using System.Text.Json.Serialization;
using System.Windows.Media;
using Dial.Services;

namespace Dial.Models;

public class MenuItemModel
{
    public string Label { get; set; } = "";
    public MenuItemType Type { get; set; } = MenuItemType.LaunchApp;
    public string Target { get; set; } = "";
    public string Arguments { get; set; } = "";
    public bool RunAsAdministrator { get; set; }
    public SystemCommand Command { get; set; } = SystemCommand.Lock;
    public string Icon { get; set; } = "";
    public bool Enabled { get; set; } = true;

    [JsonIgnore]
    public ImageSource? DisplayImage { get; private set; }

    [JsonIgnore]
    public string DisplayGlyph { get; private set; } = "";

    [JsonIgnore]
    public bool DisplayIsText { get; private set; }

    [JsonIgnore]
    public string DisplayFontFamily => DisplayIsText
        ? "Segoe UI"
        : "Segoe Fluent Icons, Segoe MDL2 Assets";

    public void RefreshDisplayIcon()
    {
        var spec = IconHelper.Resolve(this);
        DisplayImage = spec.Image;
        DisplayGlyph = spec.IsImage ? "" : spec.Glyph ?? "";
        DisplayIsText = spec.IsText;
    }
}
