# Changelog


## 1.18.0 — 2026-09-18
- Restored Spatial Sound and game session volume after profiled games stop, alongside output and input devices.
- Added per-device "Include in quick switch" so quick switch can cycle a curated device list without relying only on custom names.
- Reapplied preferred playback and recording devices when Windows endpoints reconnect, unless a game profile session is active.
- Hid Spatial Sound options on the game menu when Spatial Sound integration is disabled.
- Started media session discovery only when Media theme controls are used, instead of on every Playnite start.
- Removed unused Fullscreen-era settings and localization leftovers; settings schema is now version 2 with backward-compatible loading.

## 1.17.2 — 2026-09-06
- Fixed the Fullscreen battery widget icon so it matches the Desktop top-bar icon: fixed choice, active device icon, or the default speaker fallback.
