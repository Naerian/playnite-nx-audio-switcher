# Playnite NX Audio Switcher

Audio Switcher is a Playnite extension for controlling Windows audio devices and volume from Desktop mode, Fullscreen mode, game context menus, and custom Fullscreen themes.

It is designed for couch and console-like setups that regularly move between speakers, TVs, soundbars, headphones, microphones, capture devices, and wireless headsets.

## Features

- Switch the default Windows playback and recording devices.
- Use the extension from Playnite Desktop and Fullscreen mode.
- Rename devices, assign optional icons, hide unused entries, and set a preferred volume per device.
- Apply preferred output and input devices whenever Playnite starts.
- Optionally cycle through visible output devices with `Back + RB`.
- Create per-game profiles for output, input, Spatial Sound, and game session volume.
- Optionally bind a game volume profile to a detected audio process when automatic session detection is ambiguous.
- Restore the previous output and input devices after a profiled game closes.
- Control master output, microphone input, the current game, and background media sessions.
- Group browser sessions by application and optionally show real application icons.
- Export audio session diagnostics and back up settings and game profiles.
- Review current devices, volumes, profiles, Spatial Sound, and active sessions from the settings overview.
- Show battery level and charging state from generic Windows properties, associated Bluetooth PnP nodes, or standard HID battery controls.
- Choose exactly which informational notifications are shown.
- Integrate controller-friendly selectors, sliders, widgets, and a media mixer into Fullscreen themes.
- Use Playnite localization resource dictionaries with English fallback.

## Requirements

- Windows.
- Playnite 10.x.
- Playnite SDK 6.16 compatible runtime.

## Installation

### Playnite add-on browser

Search for **Audio Switcher** in Playnite's add-on browser, or use this direct link:

`playnite://playnite/installaddon/PlayniteAudioSwitcher_708b6ec4-bf96-4c0d-bd9d-fe0aa04d6bf1`

### Manual installation

1. Download the latest `.pext` from the [GitHub releases page](https://github.com/Naerian/playnite-nx-audio-switcher/releases/latest).
2. Open the `.pext` file or drag it into Playnite.
3. Restart Playnite if requested.

## Quick Start

Open:

`Add-ons > Extension settings > Generic > Audio Switcher`

Use the **Output** and **Input** tabs to choose optional preferred devices for Playnite startup and to customize device names, icons, visibility, and preferred volume. Choosing **Keep the Windows default** leaves that direction untouched, including when switching between Playnite Desktop and Fullscreen. Each listed endpoint shows its Windows state and a dedicated battery field; the field uses an em dash when Windows does not report a battery, so `0%` always means a real empty-battery reading. Stale inactive endpoints are hidden unless the user customized them or a game profile still references them. Game profiles can temporarily override either preferred device and restore the previous output/input after the game closes. The **Game profiles** tab includes a central manager for reviewing, editing, and removing every saved output, input, Spatial Sound, and game-volume assignment.

Under **General > Desktop**, Audio Switcher can stay available in Playnite's Desktop top bar with a fixed icon or the icon of the active output device. Under **General > Battery**, Desktop can show the icon alone, the icon plus a reported percentage, or replace the icon with the percentage while automatically restoring the icon when no battery value is available. The Fullscreen `BatteryWidget` has its own independent display mode and icon. Hovering the Desktop item shows the current output device, and clicking opens Audio Switcher settings. Notifications are available from their own top-level tab; maintenance tools and experimental Spatial Sound integration remain separate.

The bundled device icon catalog uses [Tabler Icons](https://tabler.io/icons), distributed under the MIT license included in `Icons/LICENSE-TABLER.txt`.

To switch devices:

- Desktop: `Main menu > Extensions > Audio Switcher`.
- Fullscreen: `Main menu > Extensions > Audio Switcher`.
- Per game: open the game's context menu and select `Audio Switcher`.

For games that use a launcher, emulator, or separate audio process, use `Audio Switcher > Audio process` in the game's context menu, directly below **Game volume**. Choose **Choose executable...** while the game is closed, or start it and select a detected active session. Either method creates the profile when needed; **Automatic detection** removes only that manual association.

## Documentation

- [Documentation in English](https://github.com/Naerian/playnite-nx-audio-switcher/wiki/EN-Overview)
- [Documentacion en espanol](https://github.com/Naerian/playnite-nx-audio-switcher/wiki/ES-Descripcion-General)
- [Fullscreen theme integration](https://github.com/Naerian/playnite-nx-audio-switcher/wiki/EN-Theme-Integration)
- [Theme API reference](https://github.com/Naerian/playnite-nx-audio-switcher/wiki/EN-Theme-API-Reference)
- [Troubleshooting and FAQ](https://github.com/Naerian/playnite-nx-audio-switcher/wiki/EN-Troubleshooting-and-FAQ)

## Fullscreen Theme Integration

Theme developers can use bundled controller-friendly controls:

```xml
<ContentControl x:Name="AudioSwitcher_DeviceList" />
<ContentControl x:Name="AudioSwitcher_VolumeSlider" />
<ContentControl x:Name="AudioSwitcher_InputDeviceList" />
<ContentControl x:Name="AudioSwitcher_InputVolumeSlider" />
<ContentControl x:Name="AudioSwitcher_GameVolumeSlider" />
<ContentControl x:Name="AudioSwitcher_MediaMixer" />
```

For custom layouts, Audio Switcher exposes collections, state, writable volume properties, and commands through:

```xml
{PluginSettings Plugin=AudioSwitcher, Path=CurrentDeviceName}
```

Every item in `MediaSessions` exposes writable `VolumePercent` and `Volume` properties plus `SetVolumeCommand`, `VolumeUpCommand`, `VolumeDownCommand`, and `ToggleMuteCommand`. This lets themes build a fully custom per-application mixer without first changing the globally selected media session. Audio Switcher discovers sessions created or removed while Playnite is running and updates existing objects in place to preserve controller focus.

The theme API exposes `ApiVersion` and `Supports*` capability flags so themes can conditionally enable integrations. API `1.2.0` added `SupportsDeviceBattery`, the ready-to-use `AudioSwitcher_BatteryWidget`, current output/input battery properties, and `BatteryPercent`, `BatteryLabel`, `HasBattery`, and `IsBatteryCharging` on every device item. API `1.3.0` adds `SupportsDesktopIndicatorConfiguration` and the resolved Desktop indicator icon/visibility properties. Battery values are refreshed in the background. Audio Switcher checks generic Windows container properties, associated Bluetooth PnP service nodes (including the value shown by Windows 11 Bluetooth settings), and read-only standard HID battery controls. Values remain empty when none of these safe routes exposes them. The provider chain allows safe model-specific readers to be added later without changing the theme API. Availability properties include `HasDefaultOutputDevice`, `HasDefaultInputDevice`, `IsOutputVolumeAvailable`, `IsInputVolumeAvailable`, `HasRunningGame`, `HasActiveGameAudioSession`, `HasMediaSessions`, `HasSelectedMediaSession`, `HasAudioError`, and `LastAudioError`. `Devices` and `InputDevices` remain the visible active selectors; `KnownDevices` and `KnownInputDevices` additionally expose endpoints reported by Windows as disabled, disconnected, or unavailable.

The repository and release package include a fully commented example at [`Examples/FullscreenThemeIntegration.xaml`](Examples/FullscreenThemeIntegration.xaml). The Wiki documents every bundled control, property, command, collection item, and controller focus requirement.

## Experimental Spatial Sound

Audio Switcher can optionally call a user-provided [SoundVolumeView](https://www.nirsoft.net/utils/sound_volume_view.html) or [svcl](https://www.nirsoft.net/utils/sound_volume_command_line.html) executable to apply Spatial Sound from game profiles.

The tools are not bundled or downloaded by the extension. Windows does not currently expose a stable public API for selecting and reading the active Spatial Sound format, so this integration should be treated as experimental.

## Localization

The plugin uses Playnite localization resource dictionaries under `Localization/`. Unsupported locales fall back to English. Community translation contributions are welcome.

## Support

If you find this project useful and want to support its development, consider buying me a coffee!

[![ko-fi](https://ko-fi.com/img/githubbutton_sm.svg)](https://ko-fi.com/naerian)
