# Release notes

## 1.4.11

- Run commands and scripts in Command Prompt, PowerShell, or a custom
  interpreter, with configurable argument templates.
- Choose icons from the Windows symbol gallery, image and ICO files, or icon
  resources in EXE, DLL, and ICL files.
- Keep icon browsing responsive by loading the gallery in the background and
  keeping keyboard focus in sync with the current selection.
- Add media controls for play/pause, previous track, next track, and stop.
- Send a single key or a key combination from a menu item.
- Use a screen reader to select and run named items, including in icon-only
  mode. Enter and Space both activate the selected item.
- Keep Settings within the monitor work area on small and high-DPI displays,
  keep Save visible while scrolling, and keep the window height steady when
  switching item types.
- Before pasting text, sending a shortcut, or triggering a key-based Windows
  operation, wait for held keys and mouse buttons to be released. Cancel if the
  original target window changes.
- If an icon resource is unavailable, show a fallback without discarding its
  saved path or resource index.
- Rename "System command" to "Windows operation", clarify operation names and
  ordering, and add contextual hints without changing the available actions.
- Refresh localized operation choices immediately when the language changes.
- Use a matching monochrome Windows command-line symbol for the default
  PowerShell item.

## 1.4.10

- Add an About window with the app version, author, project link, and copyright.
- Add Microsoft Store packaging with Windows-managed startup.
- Show startup restrictions in Settings and prevent overlapping saves.
- Keep the wheel's background, text, and clickable areas aligned when display
  scaling changes.
- Load icons from relative paths and use a fallback for damaged images.
- Add rotating local error logs; logs do not include clipboard text or settings.
- Fix a tray icon recovery error that could close Dial.
- Cancel a paste if focus moves away from the original window.
- Hide the unused background window when transparency is unavailable.
- Give the app icon a frosted finish and stronger outlines.

## 1.4.9

- Let the title bar follow Windows light or dark mode independently of Dial's
  theme.
- Show save results and validation messages beside Save. Hover over a long
  message to read it in full.
- Restore the center outline and keep it aligned with the Acrylic background
  while resizing.
- Translate error explanations and keep invalid settings open for correction.
- Run supported installed apps as administrator.
  Dial explains when elevation is unavailable; canceling UAC leaves it running.
- Report invalid configuration files when they are loaded or reloaded.
- Use square controls and lighter hover and keyboard-focus indicators in Settings
  and the app picker. Use square window corners on Windows 11.

## 1.4.8

- Add Windows accent color as an option and follow system accent changes without
  restarting.
- Keep the center tint and Acrylic mask in sync while resizing, and preserve
  Windows High Contrast behavior.
- Soften segment outlines while keeping them visible.
- Keep labels and icons readable during selection, with or without transparency,
  and respect Windows High Contrast colors.
- Align the Add, Remove, and reorder controls with the item list.
- Use consistent control behavior in Settings and the installed-app picker.

## 1.4.7

- Make item selection respond faster to pointer movement.
- When the center button is hidden, click the empty center to close the wheel.
- Use Windows Blue (`#357EC7`) as the light theme's accent color.
- Reduce delays the first time a wheel layout with new icon and label
  combinations is opened.
- Keep the wheel responsive when opening or closing is interrupted.

## 1.4.6

- Continue center resizing from its current size when the selection changes,
  keeping the Acrylic mask aligned.
- Set the light theme accent to `#0078D4` and the dark theme accent to
  `#4CC2FF`.

## 1.4.5

- Give the shortcut field more room and make its Set and Cancel buttons more
  compact.
- Reduce rendering overhead during open and close animations and improve text
  clarity.

## 1.4.4

- Move settings to `%LOCALAPPDATA%\dial\settings.json` instead of storing them
  in the executable. Missing settings use defaults; the file is created when
  you save. Saving no longer rewrites the executable or requires a restart.
- Make selected sectors easier to distinguish and keep the gaps between them
  even.
- Correct the left/right ordering of icons and labels.

## 1.4.3

- Scale each selected item from its own center and keep its outline and clickable
  area aligned.
- Match icon spacing to the wheel layout.

## 1.4.2

- Introduce a frosted, translucent app icon with an empty center.
- Improve label fitting on diagonal sectors, shortcut validation, and app
  shutdown reliability.
