namespace Dial.Models;

public static class MenuGlyphs
{
    public static string ForType(MenuItemType type) => type switch
    {
        MenuItemType.LaunchApp => "\uE8A9",   // AllApps
        MenuItemType.OpenFile => "\uE8A5",    // Document
        MenuItemType.OpenUrl => "\uE774",     // Globe
        MenuItemType.SystemCommand => "\uE713", // Settings
        MenuItemType.RunScript => "\uE756",   // CommandPrompt
        MenuItemType.ClipboardSnippet => "\uE77F", // Paste
        _ => "\uE713",
    };

    public static string ForCommand(SystemCommand cmd) => cmd switch
    {
        SystemCommand.Lock => "\uE72E",
        SystemCommand.Sleep => "\uE7E8",
        SystemCommand.Restart => "\uE72C",
        SystemCommand.Shutdown => "\uE7E8",
        SystemCommand.SignOut => "\uE748",
        SystemCommand.VolumeUp => "\uE767",
        SystemCommand.VolumeDown => "\uE767",
        SystemCommand.VolumeMute => "\uE74F",
        SystemCommand.Screenshot => "\uE722",
        SystemCommand.TaskManager => "\uE9D5",
        SystemCommand.ShowDesktop => "\uE8BD",
        SystemCommand.EmptyRecycleBin => "\uE74D",
        SystemCommand.OpenSettings => "\uE713",
        SystemCommand.ControlPanel => "\uE80F",
        SystemCommand.ClipboardHistory => "\uE77F",
        SystemCommand.EmojiPanel => "\uE76E",
        SystemCommand.RunDialog => "\uE756",
        _ => "\uE713",
    };
}
