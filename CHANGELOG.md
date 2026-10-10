# Changelog


## 1.18.5 — 2026-10-10
- Overview Appearance uses a ComboBox dropdown instead of preset chips, with the same card title styling as the other Overview cards.
- New Default appearance preset: Playnite TextBrush / HighlightGlyphBrush, plus theme bg/surface when the theme exposes an opaque pair (WindowBackgourndBrush/PopupBackgroundBrush or Fullscreen control brushes); otherwise derives a second surface level or falls back to Midnight (new installs default to it).
- Settings window remembers size and maximized state when reopened.

## 1.18.4 — 2026-10-08
- Replaced Playnite popup notifications with on-screen low-battery toasts styled like Controller Manager, including Soft, Compact, Bold, Arcade, Minimal, and Cinematic presets.
- Added Appearance settings for Desktop and Fullscreen looks, full toast customization, theme bridge via AudioSwitcher/theme-bridge.json, and layout packs themes can ship under AudioSwitcher/.
- Added export and import of shareable .asvisual visual profiles from Appearance > Looks, with imported designs selectable and deletable in the preset list.
- Low-battery notices fire once per discharge for the active output or input device, and stay silent when Windows does not report a battery level.

## 1.18.3 — 2026-10-02
- Added a session-oriented support log with optional detailed logging under Advanced â†’ Maintenance.
- Added Open log and Clear log actions with confirmation, matching the Metadata AI maintenance flow.

## 1.18.2 — 2026-09-21
- Made per-game profile apply and restore automatic plugin behavior instead of optional settings toggles.
- Simplified the Game profiles settings page with a live name search filter and without cover images in each row.
- Renamed the profile action to Remove and clarified the confirmation dialog wording, including correct quotation marks.

## 1.18.1 — 2026-09-21
- Moved Desktop and Fullscreen battery options into their own General tabs and removed the Battery section.
- Styled the Fullscreen battery icon note and Spatial Sound notice as info callouts.
- Replaced Spatial Sound download links with Download SoundVolumeView and Download svcl buttons.

## 1.18.0 — 2026-09-18
- Restored Spatial Sound and game session volume after profiled games stop, alongside output and input devices.
- Added per-device "Include in quick switch" so quick switch can cycle a curated device list without relying only on custom names.
- Reapplied preferred playback and recording devices when Windows endpoints reconnect, unless a game profile session is active.
- Hid Spatial Sound options on the game menu when Spatial Sound integration is disabled.
- Started media session discovery only when Media theme controls are used, instead of on every Playnite start.
- Removed unused Fullscreen-era settings and localization leftovers; settings schema is now version 2 with backward-compatible loading.

## 1.17.2 — 2026-09-06
- Fixed the Fullscreen battery widget icon so it matches the Desktop top-bar icon: fixed choice, active device icon, or the default speaker fallback.
