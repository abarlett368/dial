# dial

[Microsoft Store](https://apps.microsoft.com/detail/9NGB80VW459K) |
[Portable download](https://github.com/abarlett368/dial/releases/latest) |
[User guide](#user-guide) |
[Release notes](CHANGELOG.md)

dial is a radial launcher for Windows. Press a keyboard shortcut to open the
wheel, then choose an action with your mouse or keyboard. It can open apps,
files, folders, and URLs, run commands, paste text, or send shortcuts.

Download the portable build from GitHub or install Dial Menu from the Microsoft
Store. Store updates are delivered through the Store.

## What's new in 1.4.11

- Run commands through Command Prompt, PowerShell, or a custom interpreter.
- Choose icons from Windows symbols, image and ICO files, or EXE, DLL, and ICL resources.
- Control media playback and send a single key or key combination.
- Navigate the wheel with a keyboard or screen reader, including icons-only mode.
- Keep Settings at a consistent size, with scrolling on smaller displays.
- Continue using the menu when a saved icon resource is unavailable.

See the [1.4.11 release notes](CHANGELOG.md#1411).

## Features

- Launch desktop applications and supported Microsoft Store apps.
- Open files, folders, URLs, and application links.
- Run commands and scripts through a selected interpreter.
- Paste saved text and send keyboard shortcuts.
- Use Windows actions for media, volume, screen capture, and session controls.
- Configure up to 16 menu items and arrange or disable them individually.
- Choose a light or dark theme, accent color, and icon-and-label layout.
- Choose icons from Windows symbols, image and ICO files, or EXE, DLL, and ICL resources.
- Use the interface in English or Simplified Chinese.

## Requirements

Requires 64-bit Windows 10, version 1809 or later, or Windows 11.

## Installation

### Microsoft Store

[Get Dial Menu from the Microsoft Store](https://apps.microsoft.com/detail/9NGB80VW459K).

### Portable version

1. Download `dial.exe` from the [latest release](https://github.com/abarlett368/dial/releases/latest).
2. Save it in a folder where you want to keep the app and run it. No installer or separate .NET runtime is needed.
3. Configure the menu in Settings and select Save.
4. Press Ctrl+Tab to open the wheel.

## User guide

### Activation and navigation

Ctrl+Tab opens the wheel by default. To choose another shortcut, select Set in
Settings, enter the new combination, and save. If the combination is already
in use, choose a different one.

- Click a sector to execute its action.
- Use Right or Up to move clockwise and Left or Down to move counterclockwise.
- Enter executes the selected action.
- Esc, a center or outside click, or window deactivation dismisses the menu.

Moving the pointer outside the wheel clears the current selection. The wheel
can be opened only when at least one menu item is enabled.

### Application lifecycle

Use the notification-area menu to open Settings, reload saved changes, or exit.
Closing Settings leaves dial running. About shows the app version and project link.

### Menu configuration

Select an item in Settings to edit it. Add or remove items and use the arrow
buttons to arrange them clockwise from the top of the wheel. Up to 16 items can
be saved; disabled items remain in Settings but do not appear in the wheel.
Select Save to apply changes.

| Action | Behavior |
| --- | --- |
| Launch app | Start a desktop or supported Store app, with optional arguments |
| Open file/folder | Open a file or folder |
| Open URL | Open a website or supported app link |
| Windows operation | Run a built-in Windows action |
| Run command/script | Run a command with the selected interpreter |
| Paste text | Paste saved text into the active app |
| Send shortcut | Send a key or key combination to the active app |

### Usage details

Use Apps... to select an installed application or Browse... to choose an
executable. The Run as administrator option is available for supported desktop
apps and is handled by Windows.

To open Microsoft Store, use Apps... or the `ms-windows-store:` URL. Open
Control Panel with Windows operation > Open Control Panel or launch
`control.exe`. The built-in Settings action opens Windows Settings.

Windows operations include screen capture, clipboard tools, volume controls,
media controls, session actions, and Empty Recycle Bin. Restart, shutdown, and
sign-out run when selected. Media actions send standard Windows media keys.

Run command/script uses Command Prompt by default. You can select PowerShell or
configure another executable. In a custom argument template, `{command}` marks
where the command is inserted; without it, the command is appended.

Choose Send shortcut and select Set to record one key or a combination, such
as `F5` or `Ctrl+Shift+Tab`. Esc, Cancel, or leaving the recording controls
ends recording without applying it. Some Windows-reserved shortcuts must be
entered manually. Each item sends one key or combination.

Shortcuts are sent to the app that was active before the wheel opened. Keep
that app active. The action is cancelled if the target window changes or a
keyboard key or mouse button is still pressed. These checks also apply to
pasted text and media controls.

Names and text icons can contain up to 64 characters. Choose icon... opens a
gallery of Windows symbols. Select a symbol and choose Select, or double-click
it. Browse... opens an image or ICO file, or displays resources from an EXE,
DLL, or ICL file. Refresh returns to the Windows symbol gallery.

The gallery loads in the background and supports keyboard navigation. Its
symbols come from the Segoe Fluent Icons and Segoe MDL2 Assets fonts installed
with Windows.

Leave the icon field empty to use an automatic icon. Files and apps use their
own icons; other items use a symbol based on the action. Selecting an EXE, DLL,
or ICL resource saves its file path and resource index. If that resource is
unavailable, dial uses an automatic icon until the file is restored or another
icon is selected.

When no settings file exists, dial creates the default menu on first save.

Settings shows an error beside Save if a value cannot be saved. While a save is
in progress, editing and closing the window are paused.

The Settings window keeps a consistent size as item options change. On displays
with less available space, its content scrolls and Save stays accessible.

### Appearance

Choose a light or dark theme, a preset accent color, or Windows accent. The
wheel can show labels and icons together, labels only, or icons only. You can
also hide the center button and position the wheel near the pointer or center it
on the active monitor.

Screen readers can identify and activate items in every layout. Use Enter or
Space to activate the selected item.

Transparency and animation follow the available Windows display effects. If
Acrylic is unavailable, dial uses an opaque background. The title bar follows
Windows appearance settings independently of the app theme.

### Startup and updates

Enable Start when I sign in in Settings to run dial at login. Portable startup
uses the current executable path; disable startup before moving the file, then
enable it again from its new location.

To update the portable version, exit dial from the notification area and
replace `dial.exe`. Your settings are stored separately. For a Store
installation, Windows handles app updates. If startup does not run, check
Settings > Apps > Startup.

### Settings, backup, and reset

Settings are stored in `%LOCALAPPDATA%\dial\settings.json` for the portable
version. The file is created when you first save.

- Backup: exit dial and copy `settings.json`.
- Restore: exit dial, replace `settings.json` with the backup, then restart.
- Reset: exit dial and rename `settings.json`; dial creates a default menu.
- Reload: select Reload in the notification-area menu to restore saved settings.

An empty menu is valid. If settings cannot be read, dial reports an error and
leaves the file unchanged. Store installations may use a different settings
location from the portable version. Copy the file before uninstalling if you
want to keep your menu.

## Privacy and diagnostics

dial does not require an account and does not send usage data or error reports to
the developer. Settings are stored locally and may include item names, text,
paths, commands, and launch arguments.

Error logs are stored locally in `%LOCALAPPDATA%\dial\logs` and are not
automatically uploaded. They omit clipboard text and full settings contents.
Diagnostic trace output may include paths or application identifiers.

For more information, see the [Privacy Policy for Dial Menu](https://cdn.storeedgefd.dsx.mp.microsoft.com/wus2/privacy-policy-storage/97319480/00013648632900678756/1152921505702391275/privacy_policy_adb16b86-d64d-41c8-a54c-3477e38fed30.txt).

## Troubleshooting

| Symptom | Check |
| --- | --- |
| Hotkey does not open the wheel | Choose a shortcut that is not in use and enable at least one menu item |
| Process remains after Settings closes | Exit dial from the notification-area menu |
| The menu has no transparency | Check Windows display effects and graphics support |
| Action execution fails | Check the target, arguments, interpreter, and app permissions |
| Text insertion fails | Check that the intended app is active and accepts input |
| Settings cannot load or save | Read the displayed error and check file access |
| A custom icon is missing | Restore the source file or select another icon |
| Shortcut action is cancelled | Release any keyboard key or mouse button that is still pressed, and keep the target app active |

Run `dial.exe --settings` to open Settings directly. To use a different
settings file, close dial first, then run
`dial.exe --config "<path>\settings.json"`, replacing `<path>` with the
directory that contains the target JSON file.

Report problems through [GitHub Issues](https://github.com/abarlett368/dial/issues).
Include the app version, Windows version, and steps that reproduce the issue.

### Validation scope

Display effects and some actions depend on Windows settings, connected displays,
and installed applications.

## Uninstallation

For the portable version, turn off Start when I sign in, save, exit from the
notification-area menu, and delete `dial.exe`. To remove saved settings as well,
delete `%LOCALAPPDATA%\dial`.

Uninstall the Store version from Windows Settings > Apps.

## Build

Install the .NET 10 SDK, open a terminal at the repository root, and run:

```powershell
dotnet build .\Dial\Dial.csproj -c Release -warnaserror
dotnet publish .\Dial\Dial.csproj -p:PublishProfile=Portable -o .\publish\dial-win-x64
```

## Copyright

Copyright 2026 abarlett368. All rights reserved.

Third-party components retain their respective licenses.
