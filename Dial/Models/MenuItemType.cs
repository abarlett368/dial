namespace Dial.Models;

public enum MenuItemType
{
    LaunchApp,
    OpenFile,
    OpenUrl,
    SystemCommand,
    RunScript,
    ClipboardSnippet,
    SendShortcut
}

public enum SystemCommand
{
    Lock,
    Sleep,
    Restart,
    Shutdown,
    SignOut,
    VolumeUp,
    VolumeDown,
    VolumeMute,
    Screenshot,
    TaskManager,
    ShowDesktop,
    EmptyRecycleBin,
    OpenSettings,
    ControlPanel,
    ClipboardHistory,
    EmojiPanel,
    RunDialog,
    MediaPlayPause,
    MediaPrevious,
    MediaNext,
    MediaStop
}
