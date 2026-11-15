# dial

**dial** is a configurable radial launcher for Windows. Open it with a global
keyboard shortcut, then choose an application, file, URL, Windows operation,
script, text snippet, or key chord with the mouse or keyboard.

[Microsoft Store](https://apps.microsoft.com/detail/9NGB80VW459K) |
[Portable download](https://github.com/abarlett368/dial/releases/latest) |
[User guide](#user-guide) |
[Release notes](CHANGELOG.md) |
[Architecture](ARCHITECTURE.md)

This page describes the **1.4.11 source version**. Its portable build is
available from the latest GitHub release. Microsoft Store updates are
distributed separately.

## What's new in 1.4.11

- Choose Windows symbols in a visual icon gallery, or browse image, ICO, EXE,
  DLL, and ICL icon resources.
- Add play/pause, previous track, next track, stop, and a configurable
  **Send shortcut** action.
- Use named, keyboard-invokable radial actions with screen readers, including
  in icons-only mode.
- Keep Settings usable on small and high-DPI displays, with a scrollable body
  and a visible Save button.
- Recover from unavailable icon resources and guard key delivery against held
  keys or a changed foreground window.

See the [1.4.11 release notes](CHANGELOG.md#1411).

## Features

- Application launching, including supported Microsoft Store apps.
- File, directory, URL, and application-URI activation.
- Command execution through `cmd.exe` and clipboard-based text insertion.
- Windows system actions, including volume control, session locking, and screenshot-tool activation.
- Up to 16 configurable menu items, with ordering and per-item enable/disable state.
- Light and dark themes, preset or Windows accent colors, and four icon-and-label layouts.
- Visual icon selection from Windows symbols, image/ICO files, and EXE/DLL/ICL resources.
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
| Windows operation | Choose a built-in Windows action without writing a command |
| Run command/script | Execute a command line through `cmd.exe` |
| Paste text | Write a configured snippet to the clipboard and send Ctrl+V |
| Send shortcut | Send one key or key combination to the previously active window |

**Apps...** selects an installed application, including supported Microsoft Store
apps; **Browse...** selects an executable. **Run as administrator** is available
for eligible targets. Windows handles elevation through UAC; dial itself remains
unelevated. URI targets and some installed applications do not support elevation.

The default Microsoft Store item uses **Open URL** with `ms-windows-store:`.
This is a Windows application protocol, not a website; Windows opens the Store app.
Alternatively, use **Launch app > Apps...** to select Microsoft Store.
For Control Panel, choose **Windows operation > Open Control Panel**, or use **Launch app**
with `control.exe`. The built-in Settings action opens Windows Settings, not Control Panel.

**Windows operation** replaces the former **System command** display name;
existing configurations and execution behavior are unchanged. The list places
screen capture, desktop/clipboard tools and Windows utilities first, followed
by audio controls, session/power actions and Empty Recycle Bin.
**Toggle mute** switches between muted and unmuted; **Capture screen region**
opens Windows screen capture rather than immediately saving an image.
Contextual hints explain clipboard-history requirements and disruptive actions.
Media operations include play/pause, previous track, next track and stop; they
send Windows media keys, and the media application determines its response.
Restart, shutdown and sign-out request execution immediately without an extra
dial confirmation; save your work before activating those sectors.

### Sending shortcuts

Choose **Send shortcut**, then enter a key such as `F5` or a combination such as
`Ctrl+C`, `Ctrl+Shift+Tab` or `Win+Shift+S`. **Set** records one key or combination;
**Esc**, **Cancel**, or leaving the recording controls cancels recording.
Windows-reserved combinations may need manual entry. This is a single chord,
not a sequence, macro, or script; modifier names are Ctrl, Alt, Shift and Win.
Number keys use WPF key names such as `D1`; arrow keys use Left, Right, Up and Down.

dial restores the previous target window, waits briefly for held keyboard/mouse
buttons to be released, and cancels if that window loses focus. Input may be
blocked by administrator privileges or Windows security screens. The action
cannot use dial's activation shortcut, to avoid reopening the wheel.
The same release and target-focus checks apply to text pasting and Windows
operations that send keys, including media controls.

### Icons

Item names and custom text icons support up to 64 characters. Image files can
also be used as icons. **Choose icon...** opens a visual grid of Windows font
symbols; select a symbol and choose **Select**, or double-click it. **Browse...**
in this dialog selects an image or ICO file, or previews icon resources from
an EXE, DLL, or ICL file. **Refresh** returns to the built-in font icons.
Icons load in the background into the scrollable gallery; **Cancel** remains
available. The Windows symbol list is cached while dial is running.
The available symbols depend on the installed Segoe Fluent Icons and Segoe MDL2
Assets fonts. These symbols are Unicode characters, so they can also be copied
into the icon field.
Arrow keys and Home/End move both selection and keyboard focus; Enter confirms
the focused symbol.

An empty icon field means **automatic**, not no icon. Applications and files use
their available icons; other actions use a built-in symbol based on their type
or system command. Automatically resolved icons do not populate the editable
field. Clear a custom value to restore automatic selection.
Choosing a resource from an EXE, DLL, or ICL stores its file path and resource
index; it does not copy the icon into the configuration.

External targets and custom icon files must remain accessible at their configured
paths. If a saved icon library disappears or its selected resource changes,
dial loads the menu with an automatic icon while retaining the original path
and index. Restore the library or clear/reselect the icon before saving.
Commands and scripts are trusted configuration and
are not sandboxed. System actions may change the Windows power or session state.

The default email snippet contains the placeholder `you@example.com`.
The default PowerShell shortcut uses the monochrome Windows command-line symbol (`U+E756`).
Defaults are used when no configuration file exists. Old configuration formats
are not a compatibility target; no migration is provided.

### Saving changes

If a setting is invalid or cannot be saved, a message appears beside **Save**.
Hover over an ellipsized message to display the full text. Editing and closing
Settings are disabled while a save is pending.

Settings stays within the monitor's work area; its body scrolls when display
scaling or a small screen leaves insufficient space. Save remains in the header.

### Appearance

Settings supports light and dark themes, preset accent colors, and
**Windows accent**, which tracks the system accent color.

Four layouts are available: inner labels with outer icons, inner icons with outer
labels, labels only, or icons only. The center button is optional. Mouse-relative
placement positions the menu near the pointer; otherwise, it is centered in the
work area of the monitor under the pointer.

Screen readers receive each action's name and button invocation/selection
semantics, including in icons-only mode. Enter or Space activates the selected action.

Transparency and animation depend on Windows settings and system support. When
Acrylic is unavailable, the menu uses opaque surfaces. Window title bars follow
Windows appearance settings independently of dial's content theme.

### Startup and updates

**Start when I sign in** enables startup registration. Portable registration
references the executable path: disable startup and save before relocating the
executable, then re-enable it from the new location.

To update a portable copy, exit dial from its tray menu and replace `dial.exe`
with the newer release. Per-user configuration is stored separately.
Back up settings before replacing the executable. This version validates the
current configuration format, not old-version compatibility. To start fresh,
exit dial and rename the configuration file before launching again.

For an MSIX installation, Windows manages package installation and updates.
If startup has been disabled by Windows, check **Settings > Apps > Startup**.
dial cannot override a user or organizational-policy block.

### Settings, backup, and reset

The default configuration path is `%LOCALAPPDATA%\dial\settings.json`.
The file is created on the first successful save.

- **Backup:** exit dial and copy the configuration file.
- **Restore:** exit dial, replace the file with a current-format backup, then restart.
- **Reset:** exit dial and rename the settings file to a backup name. Start dial
  to load the eight defaults; Save creates a new file.
- **Reload:** the tray menu's Reload command reads the saved file, not
  unsaved Settings edits.

An empty item list is valid and remains empty after reload. Invalid configuration
produces an error and is not silently overwritten. Restore a current-format backup
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
  Cancellation after a clipboard write does not restore its previous contents.
- **Local input state:** dial processes keys during explicit shortcut recording
  and checks held keyboard/mouse buttons before shortcut, paste and fixed-key
  Windows operations. It does not retain a key-event history.
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
| An indexed custom icon becomes automatic | Restore its library or clear/reselect the icon before saving; unavailable indexed icons do not prevent loading |
| Sending keys is cancelled | Release held keys/buttons and keep the intended target focused; do not bypass privilege restrictions |

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
