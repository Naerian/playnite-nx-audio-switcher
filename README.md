# Playnite NX Audio Switcher

Audio Switcher is a Playnite extension for controlling Windows audio devices and volume from Desktop mode, Fullscreen mode, game context menus, and custom Fullscreen themes.

It is designed for couch and console-like setups that regularly move between speakers, TVs, soundbars, headphones, microphones, capture devices, and wireless headsets.

## Features

- Switch the default Windows playback and recording devices.
- Use the extension from Playnite Desktop and Fullscreen mode.
- Rename devices, assign optional icons, hide unused entries, and set a preferred volume per device.
- Apply a preferred output whenever Fullscreen starts.
- Optionally cycle through visible output devices with `Back + RB`.
- Create per-game profiles for output, input, Spatial Sound, and game session volume.
- Restore the previous output and input devices after a profiled game closes.
- Control master output, microphone input, the current game, and background media sessions.
- Group browser sessions by application and optionally show real application icons.
- Export audio session diagnostics and back up settings and game profiles.
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

Use the **Output** and **Input** tabs to customize device names, icons, visibility, and preferred volume. The **Fullscreen** tab contains the preferred output, optional controller shortcut, volume step, and media display options. Notification categories, maintenance tools, game profile behavior, and experimental Spatial Sound integration have their own tabs.

To switch devices:

- Desktop: `Main menu > Extensions > Audio Switcher`.
- Fullscreen: `Main menu > Extensions > Audio Switcher`.
- Per game: open the game's context menu and select `Audio Switcher`.

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

The repository and release package include a fully commented example at [`Examples/FullscreenThemeIntegration.xaml`](Examples/FullscreenThemeIntegration.xaml). The Wiki documents every bundled control, property, command, collection item, and controller focus requirement.

## Experimental Spatial Sound

Audio Switcher can optionally call a user-provided [SoundVolumeView](https://www.nirsoft.net/utils/sound_volume_view.html) or [svcl](https://www.nirsoft.net/utils/sound_volume_command_line.html) executable to apply Spatial Sound from game profiles.

The tools are not bundled or downloaded by the extension. Windows does not currently expose a stable public API for selecting and reading the active Spatial Sound format, so this integration should be treated as experimental.

## Localization

The plugin uses Playnite localization resource dictionaries under `Localization/`. Unsupported locales fall back to English. Community translation contributions are welcome.

## Support

If you find this project useful and want to support its development, consider buying me a coffee!

[![ko-fi](https://ko-fi.com/img/githubbutton_sm.svg)](https://ko-fi.com/naerian)
