# Release notes

## 1.4.8

- Update the center tint and acrylic mask together during resizing to avoid
  the unblurred translucent phase.
- Soften sector outlines in light and dark themes while keeping them visible; remove the
  separate acrylic center outline, preserving Windows High Contrast behavior.
- Merge the two sector fill layers into one accent-tinted fill, with equivalent
  background transmission to within 8-bit alpha rounding.
- Add Windows accent as the second color option. Follow system accent
  changes without restarting, adapting the tint for fixed theme text colors.
- Keep the material clip inside the painted sector edge and remove the redundant
  WPF silhouette clip. Preserve antialiasing when applying the native input region.
- Use neutral Add and list-selection styles without focus-driven layout changes.
  Share control behavior between Settings and the installed-app picker, retaining
  square corners in Settings.
- Preview the chosen accent on Save without applying or saving the draft.
- Align the Add/Remove/reorder toolbar with the list width, keeping arrow buttons square.
- Align color transitions with sector enlargement using the
  same 90 ms quintic ease-out timing. Interrupted transitions continue from
  their current color and scale.
- Slightly soften palette saturation without changing light Windows Blue #357EC7.
- Keep the theme's label and glyph colors unchanged during selection in both
  acrylic and opaque modes. Retune theme-specific palette colors for readable
  contrast; retain light-theme Windows Blue #357EC7 as the fixed-palette exception.
  Windows High Contrast continues to use the system highlight text color.
- Replace independently clipped sector material visuals with one stationary
  backdrop sampler and a combined animated clip. This targets the transient
  gray/white fill and dark sector edges captured at window boundaries.
- Add continuous-frame capture during rapid pointer sweeps and recovery, with
  native-composition checks and neutral-fill comparisons against the background.

## 1.4.7

- Keep sector material visuals stationary while scaling their clips. This
  change did not eliminate the transient window-boundary rendering defect.
- Reduce pointer-selection work to the sectors that change and shorten sector
  selection animations for a more responsive dial.
- Reuse fixed acrylic sector masks and hide the center mask while its disk is
  resizing. This workaround introduced an unblurred translucent phase, addressed
  in 1.4.8.
- Make the full transparent center moat dismiss the dial when the center button
  is disabled.
- Use the correct Windows Blue color #357EC7 as the accent color in the light theme.
- Add rapid-selection, center-mask, and hidden-center input-region regressions.
- Resolve sector outline geometry once per layout search to avoid multi-second
  stalls on uncached icon/label arrangements.
- Activate the wheel after preparing its transparent entry frame and guard
  presentation callbacks against interrupted entry/dismissal generations.

## 1.4.6

- Continue center resizing from the current radius when a new selection
  interrupts the animation. Keep the Acrylic mask in sync.
- Set the accent color to #0078D4 in the light theme.
- Set the accent color to the brighter blue #4CC2FF in the dark theme.
- Add regression checks for interrupted resizing and mask alignment.

## 1.4.5

- Widen the shortcut field and reduce the fixed Set/Cancel button to 48 DIPs.
- Remove whole-canvas bitmap caching during entry and exit animations.
- Use grayscale text rendering and animated text hinting.
- Verify long shortcuts, localized button labels, and standalone startup and save
  behavior in English and Simplified Chinese.

## 1.4.4

- Moved settings to `%LOCALAPPDATA%\dial\settings.json`.
- Missing settings use defaults without creating a file until Save.
- Removed executable-embedded settings and save/restart behavior.
- Increased whole-sector selection growth to 3%, with balanced 5-DIP gaps.
- Matched icon separators to runtime geometry.
- Corrected mirrored left/right icon and label ordering.

## 1.4.3

- Centered selection scaling on each sector rather than the wheel.
- Added selected-outline growth and matching native hit regions.
- Updated icon separators to shared runtime proportions.

## 1.4.2

- Introduced the approved frosted translucent icon with an empty center.
- Improved diagonal label fitting, shutdown behavior, shortcut validation,
  and boundary coverage.
- Fixed native material/content window ordering and dispatcher-queue lifetime.
