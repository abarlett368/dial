# dial

**dial** is a radial launcher for Windows. Open it with a keyboard shortcut, then
select an action with your mouse or keyboard.

[Download the latest release](https://github.com/abarlett368/dial/releases/latest) ·
[简体中文](README.zh-CN.md) ·
[User guide](docs/USER-GUIDE.md) ·
[Architecture](ARCHITECTURE.md) ·
[Release notes](CHANGELOG.md)

## Get started

1. Download `dial.exe` from [Releases](https://github.com/abarlett368/dial/releases).
2. Save it in a folder you plan to keep, then run it. The portable executable
   includes .NET 10; no installer or separate .NET installation is required.
3. On first launch, edit the example actions in Settings and select **Save**.
4. Press **Ctrl+Tab** to open the dial.

The app requires 64-bit Windows 10, version 1809 or later, or Windows 11.

## Use the dial

- Point to a sector and click to run its action.
- Use the arrow keys to change the selection; press **Enter** to run it.
- Press **Esc**, click the center, or click outside the dial to dismiss it.
- Move the pointer outside the dial to reset the selection.
- To change the shortcut or actions, open Settings from the notification area.
  Closing Settings leaves dial running. Use its notification-area menu to exit.

The dial supports up to 16 actions. Configure an action to open an app, file,
folder, or URL; run a Windows command or script; or paste saved text. You can
also choose a theme and an icon-and-label layout. Supported executable actions
and installed apps such as Windows Terminal can request administrator access
through Windows User Account Control (UAC).

Settings are stored per user in
`%LOCALAPPDATA%\dial\settings.json`. They remain in place when you replace the
executable with an update. Acrylic effects depend on Windows transparency
settings and graphics support; when Acrylic is unavailable, the dial uses opaque
sectors with transparent gaps.

The light theme uses the correct Windows Blue color #357EC7 as its accent color.
The dark theme uses the brighter blue #4CC2FF as its accent color.

## Build

Install the .NET 10 SDK on Windows. From the repository root, run:

```powershell
dotnet build .\Dial\Dial.csproj -c Release -warnaserror
dotnet publish .\Dial\Dial.csproj -p:PublishProfile=Portable -o .\publish\dial-win-x64
```

The public repository contains application source, required resources, and
user-facing documentation. The maintainer keeps the test harness, icon generator,
and release automation locally. Release validation includes interactive desktop
checks and standalone executable verification. Generated files and personal
settings are not part of the repository.

## Project documentation

- [User guide](docs/USER-GUIDE.md)
- [Architecture](ARCHITECTURE.md)
- [Release notes](CHANGELOG.md)

## Copyright

Copyright © 2026 abarlett368. All rights reserved. No license is granted to use,
modify, or distribute this software.
