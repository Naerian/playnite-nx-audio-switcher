# Changelog





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
