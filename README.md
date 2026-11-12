# dial

**dial** is a Windows radial launcher implemented in C# with WPF and Win32
integration. A configurable global hotkey opens a menu of application, file,
URI, system-command, and text-snippet actions, with mouse and keyboard navigation.

[Microsoft Store](https://apps.microsoft.com/detail/9NGB80VW459K) |
[Portable download](https://github.com/abarlett368/dial/releases/latest) |
[User guide](#user-guide) |
[Release notes](CHANGELOG.md) |
[Architecture](ARCHITECTURE.md)

## Features

- Application launching, including supported Microsoft Store apps.
- File, directory, URL, and application-URI activation.
- Command execution through `cmd.exe` and clipboard-based text insertion.
- Windows system actions, including volume control, session locking, and screenshot-tool activation.
- Up to 16 configurable menu items, with ordering and per-item enable/disable state.
- Light and dark themes, preset or Windows accent colors, and four icon-and-label layouts.
- English and Simplified Chinese localization.

## Installation

Requires 64-bit Windows 10, version 1809 or later, or Windows 11.

### Microsoft Store

dial is available on the [Microsoft Store as **Dial Menu**](https://apps.microsoft.com/detail/9NGB80VW459K).
The application itself continues to use the name **dial**.

### Portable version

1. Download `dial.exe` from the [latest release](https://github.com/abarlett368/dial/releases/latest).
2. Place the executable in a persistent directory and run it. The build is self-contained; no installer or separate .NET runtime is required.
3. Configure the initial menu in Settings and select **Save**.
4. Press **Ctrl+Tab**, the default activation shortcut.

The portable executable is not Authenticode-signed. Windows may display a
SmartScreen or **Unknown publisher** warning. Use the repository's release assets
and evaluate any security warning before execution; do not disable Windows
security controls to suppress it.

## User guide

### Activation and navigation

The default global hotkey is **Ctrl+Tab**. To change it, select **Set** in Settings,
record a key combination, and select **Save**. **Cancel** or **Esc** cancels
recording. Registration fails if another application already owns the combination.

- Click a sector to execute its action.
- **Right/Up** selects the next item clockwise; **Left/Down** selects the previous item.
- **Enter** executes the selected action.
- **Esc**, a center or outside click, or window deactivation dismisses the menu.

Moving the pointer outside the wheel clears its selection. The center area still
dismisses the menu when the center button is hidden. No menu opens if there are
no enabled items.

### Application lifecycle

The notification-area menu provides **Settings**, configuration reload, and
**Exit** commands. Closing Settings does not terminate the application.
**About** displays the application version and project information.

### Menu configuration

Select an item in Settings to edit it. Use **Add**, **Remove**, and the arrow
buttons to arrange the list. Items appear clockwise from 12 o'clock.

The configuration supports up to 16 items, including disabled entries. Disabled
items are persisted but excluded from the wheel. Changes are applied on **Save**;
discarding unsaved edits leaves the active configuration unchanged.

| Action | Behavior |
| --- | --- |
| Launch app | Launch an executable or supported installed app, with optional arguments |
| Open file/folder | Open a file or directory through Windows Shell |
| Open URL | Activate an absolute URL or supported application URI |
| System command | Execute a built-in Windows action |
| Run command/script | Execute a command line through `cmd.exe` |
| Paste text | Write a configured snippet to the clipboard and send Ctrl+V |

**Apps...** selects an installed application, including supported Microsoft Store
apps; **Browse...** selects an executable. **Run as administrator** is available
for eligible targets. Windows handles elevation through UAC; dial itself remains
unelevated. URI targets and some installed applications do not support elevation.

Item names and custom text icons support up to 64 characters. Image files can
also be used as icons. External targets and icon resources must remain accessible
at their configured paths. Commands and scripts are trusted configuration and
are not sandboxed. System actions may change the Windows power or session state.

The default email snippet contains the placeholder `you@example.com`.

If a setting is invalid or cannot be saved, a message appears beside **Save**.
Hover over an ellipsized message to display the full text. Editing and closing
Settings are disabled while a save is pending.

### Appearance

Settings supports light and dark themes, preset accent colors, and
**Windows accent**, which tracks the system accent color.

Four layouts are available: inner labels with outer icons, inner icons with outer
labels, labels only, or icons only. The center button is optional. Mouse-relative
placement positions the menu near the pointer; otherwise, it is centered in the
work area of the monitor under the pointer.

Transparency and animation depend on Windows settings and system support. When
Acrylic is unavailable, the menu uses opaque surfaces. Window title bars follow
Windows appearance settings independently of dial's content theme.

### Startup and updates

**Start when I sign in** enables startup registration. Portable registration
references the executable path: disable startup and save before relocating the
executable, then re-enable it from the new location.

To update a portable copy, exit dial from its tray menu and replace `dial.exe`
with the newer release. Per-user configuration is stored separately.

For an MSIX installation, Windows manages package installation and updates.
If startup has been disabled by Windows, check **Settings > Apps > Startup**.
dial cannot override a user or organizational-policy block.

### Settings, backup, and reset

The default configuration path is `%LOCALAPPDATA%\dial\settings.json`.
The file is created on the first successful save.

- **Backup:** exit dial and copy the configuration file.
- **Restore:** exit dial, replace the file with a compatible backup, then restart.
- **Reset:** exit dial and rename the settings file to a backup name. Start dial
  to load the eight defaults; Save creates a new file.
- **Reload:** the tray menu's Reload command reads the saved file, not
  unsaved Settings edits.

An empty item list is valid and remains empty after reload. Invalid configuration
produces an error and is not silently overwritten. Restore a compatible backup
or rename the file to restore defaults.

Windows may redirect application data for MSIX installations. Do not assume a
Store installation and a portable copy share the same files. Back up settings
before uninstalling.

## Privacy and diagnostics

The application requires no account. It does not include advertising, behavioral
analytics, or automatic uploads of usage or error reports to the developer.

- **Unencrypted configuration:** JSON settings may contain personal text, local
  paths, commands, and launch arguments. Text snippets are not secure credential
  storage. Review configuration files before disclosure.
- **Clipboard writes:** text insertion replaces the clipboard contents. Windows
  clipboard history or synchronization may retain the text. Pasting is cancelled
  if the foreground target changes, and Windows may block input to an elevated process.
- **Network resources:** external applications, URI handlers, and scripts may
  initiate network access. Resolving configured network paths or loading their
  icons may trigger Windows network access or authentication before item activation.

Local error logs are written to `%LOCALAPPDATA%\dial\logs\errors.log`, with one
`errors.log.previous` backup. Each file is limited to 1 MiB. These rotating files
contain diagnostic metadata and call stacks, not clipboard text, complete
configuration contents, or exception messages. They are not automatically uploaded.

Separately, diagnostic trace output can include exception messages, paths, or
application identifiers and may be captured by a debugger or diagnostic listener.
The rotating-log exclusions do not apply to all diagnostic output.

Review and redact logs, screenshots, and other attachments before sharing them.

## Troubleshooting

| Symptom | Check |
| --- | --- |
| Hotkey does not activate the menu | Check for a registration conflict and verify that at least one item is enabled |
| Process remains after Settings closes | Expected behavior; use the notification-area **Exit** command |
| The menu has no transparency | Check Windows transparency and high-contrast settings; graphics or remote-session limitations may apply |
| Action execution fails | Verify the target path or URI, arguments, permissions, and installed application |
| Text insertion fails | Verify foreground-window focus and the target process's privilege level |
| Settings cannot load or save | Check the reported error, file format, permissions, and whether the file is locked |

For diagnostics, `dial.exe --settings` opens Settings. An explicit
`dial.exe --config "C:\path\settings.json"` uses another profile; exit the existing
instance first because only one instance runs per Windows session.

Report problems through [GitHub Issues](https://github.com/abarlett368/dial/issues).
Include the app version, Windows version, and steps to reproduce the problem.
Do not post sensitive settings or personal information.

### Validation scope

Validation remains incomplete for:

- Installed-MSIX lifecycle, startup, elevation, and configuration migration.
- Windows 11-specific behavior, physical mixed-DPI displays, and multi-GPU systems.
- Long-duration stability under unattended operation.

## Uninstallation

For a portable copy, disable **Start when I sign in** and save, exit from the tray
menu, then delete the executable. Back up the configuration before optionally
removing `%LOCALAPPDATA%\dial`. The application does not install a Windows service.

For an MSIX installation, uninstall through **Windows Settings > Apps**.

## Build

Install the .NET 10 SDK on Windows. From the repository root, run:

```powershell
dotnet build .\Dial\Dial.csproj -c Release -warnaserror
dotnet publish .\Dial\Dial.csproj -p:PublishProfile=Portable -o .\publish\dial-win-x64
```

For implementation details, see [Architecture](ARCHITECTURE.md).
Changes between versions are listed in the [release notes](CHANGELOG.md).

## Copyright

Copyright 2026 abarlett368. All rights reserved.

Third-party components retain their respective licenses.
