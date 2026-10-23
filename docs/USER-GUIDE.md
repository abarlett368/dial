# User guide

[Home](../README.md) | [简体中文](USER-GUIDE.zh-CN.md)

## First launch and updates

Run `dial.exe` from a permanent folder. Settings opens automatically if no
configuration exists. Choose Save to persist your configuration. Before replacing
the executable with an update, exit dial from its notification-area menu.
Replacing the executable preserves per-user settings.

If launch at sign-in is enabled and you move the executable, disable that setting,
save, move the program, then enable it and save again from the new location.

## Open, select, and dismiss

The default shortcut is Ctrl+Tab. In Settings, choose Set next to the shortcut
field, press the desired combination, then Save. Cancel or Esc abandons recording.
The shortcut is temporarily unregistered during recording. Another application's
shortcut registration may conflict; dial reports the conflict.

The dial normally opens in the work area of the monitor under the pointer.
Enable mouse placement to open near the pointer, constrained to that work area.

- Click a sector to execute its action.
- Right/Up selects the next item clockwise; Left/Down selects the previous item.
  Enter executes the selection.
- Moving the pointer out of the dial resets selection.
- Esc, the center, an outside click, or losing focus dismisses the dial.
  Hiding the center does not disable dismissal at its position.

An empty list, or a list without enabled items, does not open a dial.

## Configure items

Select an item to edit it. Add, Remove, and the arrow buttons change the list.
The ring starts at 12 o'clock and follows list order clockwise. Up to 16 items are
supported. Disabled items remain saved but are omitted from the dial.
Unsaved edits do not change the active configuration. Save previews the chosen
accent color; list selection stays neutral gray and does not change row size.

| Action | Use |
| --- | --- |
| Launch app | Select an executable or an installed app; optional arguments |
| Open file/folder | Select a document or folder to open using Windows |
| Open URL | Enter an absolute URL or supported application URI |
| System command | Choose a built-in Windows action |
| Run command/script | Configure a command you trust |
| Paste text | Replace the clipboard with a snippet and send Ctrl+V |

The installed-app picker includes supported Microsoft Store apps. App identities
and URI shortcuts cannot request elevation. For supported executable launches,
Run as administrator requests Windows UAC consent; dial itself remains unelevated.

Names and custom text icons support up to 64 characters. Icons may also use image
files. Keep external targets and icon files available at their configured paths.
Only configure scripts and command targets you understand and trust.
System power/session commands can interrupt work; do not use them as test items.

The Paste email example contains `you@example.com`; replace it before use.
Paste text overwrites the clipboard. Windows can block injected input into a
higher-privilege application.

## Appearance

Choose a light or dark theme, an accent color, and either English or Simplified
Chinese as the interface language. Layouts are:
outer icons/inner labels (default), inner icons/outer labels, labels only, or
icons only. The optional center adapts to selected names within a bounded size.
Extremely long words may still be ellipsized to preserve the sector boundary.

Selection keeps the theme's text color unchanged. Accent variants are tuned for
dark text in the light theme and white text in the dark theme. Light-theme
Windows Blue deliberately retains #357EC7 even though its text contrast is lower.
Acrylic selection uses a stronger color tint with equivalent background transmission; the
background can still affect its displayed color. High Contrast uses the system's
highlight text color.

The second color option, **Windows accent**, follows the Windows accent
color and updates automatically when that color changes, including while the
dial is open. Its tint is lightened or darkened when necessary to keep the
theme's text color unchanged and readable.

System transparency, high contrast, and animation preferences affect rendering.
The fallback intentionally retains clear spaces between sectors.

## Settings, backup, and reset

The normal file is `%LOCALAPPDATA%\dial\settings.json`. Open that location using
the File Explorer address bar. It is per-user and is not synchronized or stored
inside the executable. No directory or file is created until Save.

- **Backup:** exit dial and copy the settings file to a safe location.
- **Restore:** exit dial, replace the file with a compatible backup, then restart.
- **Reset:** exit dial and rename the settings file to a backup name. Start dial
  to load the eight defaults; Save creates a new file.
- **Reload:** the notification-area Reload command reads the saved file, not
  unsaved Settings edits.

A saved empty item list stays empty. Malformed settings produce an error and are
not silently overwritten; restore a backup or rename the file to reset.
The file can contain private snippets, command arguments, and local paths.
Do not upload it to the source repository.

## Troubleshooting and removal

| Symptom | Check |
| --- | --- |
| Shortcut does not open the dial | Resolve the reported shortcut conflict; check enabled items |
| Closing Settings leaves a process | Expected: use the notification-area Exit command |
| No Acrylic | Check Windows transparency/high contrast, remote-session and graphics limitations |
| An action fails | Check its target, permissions, arguments, and installed application |
| Text moves with slight softness | Fractional scaling uses grayscale antialiasing; entry/exit no longer resample a whole-canvas bitmap |
| Settings cannot load/save | Check file format, permissions, and whether the file is locked |

For diagnostics, `dial.exe --settings` opens Settings. An explicit
`dial.exe --config "C:\path\settings.json"` uses another profile; exit the existing
instance first because only one instance runs per Windows session.

To remove dial, disable launch at sign-in and Save, exit, then delete the
executable. Optionally delete `%LOCALAPPDATA%\dial` after backing up settings.
There is no installed service or installer to remove.
