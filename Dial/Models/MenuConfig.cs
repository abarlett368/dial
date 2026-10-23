namespace Dial.Models;

public class MenuConfig
{
    public string Hotkey { get; set; } = "Ctrl+Tab";
    public string Theme { get; set; } = "Light";
    public string HoverColor { get; set; } = "windowsBlue";
    public string Language { get; set; } = "en";
    public bool ShowAtMouse { get; set; } = false;
    public IconMode IconMode { get; set; } = IconMode.IconOutside;
    public bool ShowCenter { get; set; } = true;
    public bool LaunchAtStartup { get; set; } = false;
    public List<MenuItemModel> Items { get; set; } = new();
}
