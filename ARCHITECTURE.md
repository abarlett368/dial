# Architecture

## Platform

dial is a WPF desktop application for Windows 10, version 1809 or later, and
Windows 11. It targets .NET 10 and ships as a self-contained Windows x64
executable. WPF trimming is disabled. The application does not require an
installer, background service, WebView, or third-party UI framework.

## Application lifetime

[`App.xaml.cs`](Dial/App.xaml.cs) starts one application instance per Windows
session. A named mutex identifies the owner; a named event brings the existing
instance to Settings when another copy starts.

The owner loads settings, applies the saved language and theme, then creates one
radial window, hotkey handler, and notification-area icon. Closing Settings
does not exit the process. The notification-area Exit command performs normal
cleanup.

[`HotkeyService`](Dial/Services/HotkeyService.cs) uses `RegisterHotKey` and
`MOD_NOREPEAT`; it does not poll the keyboard. A replacement shortcut is
registered before the previous one is released. A conflict leaves the previous
shortcut active and reports an error.

## Settings

[`ConfigService`](Dial/Services/ConfigService.cs) stores per-user settings in
`%LOCALAPPDATA%\dial\settings.json`. Missing files load defaults without
creating a directory. Save validates the complete configuration, writes a
same-directory temporary file, and replaces the previous file. Invalid input
and malformed JSON are reported; existing data is not silently overwritten.

Settings edits are drafts. Save validates and persists the draft before
applying it. The optional startup entry contains the current executable path.
Tests use `--config <path>` to isolate configuration from the user's profile.

The configuration contains the hotkey, theme, language, accent, item layout,
center visibility, startup choice, and up to 16 actions. The first-run defaults
are This PC, Microsoft Store, Windows PowerShell, Screenshot, Bing, Paste email,
Task Manager, and Settings.

## Dial layout and rendering

[`DialGeometry`](Dial/Models/DialGeometry.cs) defines the shared layout used by
the WPF dial, hit testing, native material masks, and icon generator.

| Dimension | Value |
| --- | ---: |
| Canvas | 318 × 318 DIPs |
| Outer radius | 143 DIPs |
| Ring inner radius | 76 DIPs |
| Ring width | 67 DIPs |
| Sector gap | 5 DIPs |
| Resting center radius | 26 DIPs |
| Maximum center radius | 69.25 DIPs |
| Selected-sector scale | 1.03 |

Sector zero starts at 12 o'clock; items proceed clockwise. Selection scales each
sector around its own bounds. The center radius animates independently of layout;
content is measured before animation, not from the per-frame callback. The
center's native material mask and native tint update together throughout resize.
Layout searches resolve each sector's combined geometry to an outlined path once,
instead of evaluating all Boolean cuts for every candidate rectangle. Exact
content-clearance checks remain in place.

[`RadialWindow`](Dial/RadialWindow.xaml.cs) is a transparent, topmost WPF window.
When system backdrop composition is available, [`NativeBackdrop`](Dial/Services/NativeBackdrop.cs)
creates a native material host behind it. One stationary material visual serves
all sectors and the visible center disk through a shared composition geometry
clip. Center tint is also rendered natively and submitted with the same mask
update; WPF only draws its text and invisible interaction geometry in acrylic
mode. This avoids a WPF fill expanding before the native blur catches up.
Clear gaps remain transparent.
When the material is unavailable or disabled, opaque fills
use the same geometry.

Sector material contours are flattened once with 0.05-pixel tolerance and reused.
The material clip is inset half a physical pixel from the resting painted
outline, rather than extending to the largest selection stroke. Soft native
clip borders avoid exposing untinted material at the antialiased fringe.
WPF paths draw their silhouettes directly, without a second root geometry clip
that would multiply their edge coverage. Item content retains its sector clip.
Selection changes apply the same uniform scale to WPF and the cached native
contours. Changes are coalesced into one combined clip update per WPF rendering
frame, without rebuilding the Boolean sector geometry. Keeping one stationary
sampler for the whole ring avoids the transient black/white background sampling
observed with independently animated sector clips at window boundaries. Merely
keeping the individual visuals stationary did not eliminate that failure.
The shared mask preserves sector geometry and transparent gaps.
Only the outgoing and incoming sectors start selection animations. The center
sampler stays visible during resizing; its tint and material retire together on
shrink, rather than leaving a material ring or an unblurred translucent phase.
With the center button disabled, the native hit region
covers the full transparent moat and a click anywhere inside it dismisses the
dial.

The material host and WPF window must remain adjacent in the window order. The
host is resynchronized after window-position changes. Native hit regions use a
geometric union of resting sectors and the current selected sector; overlapping
alternate-fill figures can otherwise create holes. Contours are cached by
selection. A two-pixel integer-region guard preserves vector antialiasing without
enlarging the visible shapes. Sector motion temporarily clears the hard region;
the latest animation completion restores it. Stale completions are ignored.

The wheel fades and scales during entry and exit. The center resizes over
210 milliseconds; sector selection uses a local 3% scale change over 90 ms.
Interrupted animations continue from their current value. The content is not
cached as a whole-window bitmap during motion. Text uses grayscale antialiasing
and animated hinting.
The window is shown without activation while its transparent frame is prepared,
then activated when entry presentation starts. Presentation-generation checks
prevent stale animation callbacks from modifying a newer entry or dismissal.

## Themes and language

[`ThemeService`](Dial/Services/ThemeService.cs) defines light and dark theme resource
dictionaries. The light theme uses the correct Windows Blue color #357EC7 as its
primary accent color. The dark theme uses the brighter blue #4CC2FF as its primary
accent color. High Contrast mode uses Windows system colors and disables
transparency-dependent effects. Translucent colors are checked against light
and dark desktop backgrounds.

Each sector uses one WPF fill, not a neutral undercoat plus highlight overlay.
Resting alpha remains 208/255 in light mode and 224/255 in dark mode. Selected
alpha is the rounded equivalent of the previous two-layer combination,
`255 - (255 - restingAlpha) * (1 - 72/255)`: 221/255 and 233/255 respectively.
Measured black-to-white desktop contribution is approximately 18.4%/13.3%
in light mode (rest/selected), and 12.2%/8.6% in dark mode. These high tint
opacities can appear nearly opaque, especially for a selected dark-theme sector;
the edge-rendering changes do not lower background transmission.
Background transmission changes by at most half an 8-bit alpha step while the
accent remains free of a neutral undercoat. Tint and enlargement share a 90 ms quintic ease-out
transition on entry and exit. Interrupted transitions start from their current
displayed values, not from the neutral or fully selected endpoint.
Labels and glyphs retain the
theme foreground during selection in both acrylic and opaque modes. Palette
variants are lighter for dark text in the light theme and darker for white text
in the dark theme.
The light-theme Windows Blue swatch deliberately retains #357EC7 as the requested
exception to the 4.5:1 selected-text contrast target. High Contrast still uses the
system highlight foreground. Background content influences the final displayed
color; transparency cannot reproduce an identical opaque swatch over every
background.

The second palette option follows the current Windows accent, queried through
`UISettings.GetColorValue(Accent)`. `ColorValuesChanged` dispatches theme refresh
to the WPF thread so an open dial updates without restart; future openings also
resolve the current value. The saved key is `systemAccent`, not a color snapshot.
The system tint is blended toward white or black only when needed to preserve
fixed theme text contrast. The adjustment targets 4.8:1 against the worst-case
background at the selected alpha, retaining headroom above 4.5:1 after rounding.
The standard theme foreground stays fixed outside Windows High Contrast.

Normal outlines retain the fill's tint and alpha, darkened by 12/255 in light
mode or lightened by 18/255 in dark mode. Leaving the outline nearly transparent
would expose untinted native blur around the fill and produce a contrasting rim.
Outline tint follows the same selection animation. Acrylic center tint has no
separate outline. High Contrast keeps system outlines unchanged.

[`Controls.xaml`](Dial/Resources/Controls.xaml) shares neutral buttons, gray list
selection, input borders, and non-layout-changing keyboard focus visuals
between Settings and the installed-app picker. Settings overrides the shared
corner resources to zero; the installed-app picker retains its rounded controls.
Save previews the selected palette
color with a contrasting button label. This window-local preview does not change
the active dial or persist the draft. System-color refreshes update it through
the normal appearance refresh path.

[`LocalizationService`](Dial/Services/LocalizationService.cs) loads English
strings from [`Resources.resx`](Dial/Resources/Resources.resx) and Simplified
Chinese strings from
[`Resources.zh-CN.resx`](Dial/Resources/Resources.zh-CN.resx). Both files must
contain the same resource keys. Keep new interface text in these resource files.

## Actions and Windows integration

[`ActionExecutor`](Dial/Services/ActionExecutor.cs) handles application
launches, files and folders, URLs, system commands, scripts, and clipboard
snippets. Configured commands and scripts are local user input and run with the
user's permissions. Elevation is an opt-in property of supported executable
launches; Windows displays the UAC prompt. The dial process itself remains
unelevated.

[`InstalledAppService`](Dial/Services/InstalledAppService.cs) enumerates Windows
applications and activates supported packaged apps. [`TrayIconService`](Dial/Services/TrayIconService.cs)
uses `Shell_NotifyIcon` and restores its icon after Explorer restarts.
[`StartupService`](Dial/Services/StartupService.cs) manages the current user's
Run registry entry.

## Source and validation

| Path | Responsibility |
| --- | --- |
| [`Dial/Models`](Dial/Models) | Geometry, configuration models, actions, and accent palette |
| [`Dial/Services`](Dial/Services) | Persistence, actions, localization, themes, and Windows APIs |
| [`Dial/Resources`](Dial/Resources) | English and Simplified Chinese strings |

The maintainer retains the STA regression harness, icon generator, and publishing
automation locally; they are not included in the public repository. Local checks
cover geometry, typography, animation interruption, native materials, input
regions, settings, and standalone process behavior. They do not execute destructive
system commands or arbitrary configured scripts. Full regressions and standalone
package verification must pass before a Release is published. Only the portable
executable is attached; checksum files are not Release assets.

Window-boundary checks capture frames during pointer sweeps and for the following
half-second or longer, not only after selection settles. They require native
composition to be active, retain the captured frames, and compare neutral,
text-free interior samples against their original background brightness. This
detects an unselected sector temporarily turning gray or white even when another
sector is selected; stable-frame highlight and stroke-property checks alone do
not cover that failure.

Center-resize checks first verify that their black/white stripe background is
actually presented, then inspect captured frames inside the painted disk while
it grows and shrinks. Restoring the independent WPF center tint reproduces an
unblurred fringe in this check; submitting native tint and blur masks together
removes that fringe.

Sector-edge checks compare the captured silhouette with a host-only stripe
background and inspect selection-animation frames for untinted fringes.
Antialiasing checks ensure soft edge pixels remain inside the native input region,
without depending on identical rasterization between GPU and offscreen renderers.

Build the public source on Windows with the .NET 10 SDK:

```powershell
dotnet build .\Dial\Dial.csproj -c Release -warnaserror
```

Automated visual checks were run on Windows 10 with 150% DPI. They do not replace
validation on other GPUs, Windows 11 systems, remote sessions, or physical
mixed-DPI monitor setups.
When ordinary standalone WPF background windows failed to present correctly on
this machine's hardware path, the complete regression was also run with WPF
software presentation in the test process. Native acrylic composition remained
enabled. This test setting is not applied to the shipped application and does
not establish that the hardware presentation issue is resolved.
