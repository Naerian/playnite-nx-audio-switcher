# Playnite NX Audio Switcher

Playnite NX Audio Switcher is a Playnite extension for quickly switching the default Windows audio output device from Desktop mode, Fullscreen mode, game context menus, controller shortcuts, and custom Playnite themes.

It is designed for couch and console-like setups where users often move between TV speakers, headphones, soundbars, wireless headsets, capture devices, or controller audio outputs.

## Features

- Switch the default Windows playback device from Playnite.
- Works from both Desktop and Fullscreen mode extension menus.
- Fullscreen quick switch with `Back + Y`.
- Custom names for any number of audio devices.
- Optional icon per renamed audio device.
- Configurable Fullscreen display mode:
  - Text
  - Text and icon
  - Icon only
- Full device selector for manual switching.
- Game-specific audio profiles from the game context menu.
- Optional restore of the previous audio device after closing a game-specific profile.
- Optional preferred audio device when Playnite starts in Fullscreen mode.
- Theme integration controls for theme authors.
- Localizable UI through Playnite resource dictionaries.

## Requirements

- Windows.
- Playnite 10.x.
- Playnite SDK 6.16.0 compatible runtime.

The plugin changes the Windows default playback endpoint using Core Audio / policy configuration APIs.

## Installation

Download the `.pext` package from releases and install it in Playnite.

For manual installation during development:

1. Build the project in Release mode.
2. Copy the build output into a folder under Playnite's `Extensions` directory.
3. Restart Playnite.

## Configuration

Open:

`Add-ons > Extension settings > Generic > Audio Switcher`

From there you can:

- Give friendly names to audio devices.
- Assign icons to devices.
- Choose how devices are displayed in Fullscreen/theme UI.
- Choose a preferred Fullscreen device.
- Enable or disable the Fullscreen quick switch shortcut.
- Choose whether the quick menu should show only renamed devices.
- Configure restore behavior after game-specific profiles.

Devices without a custom name still appear in the full output selector, but only renamed devices are used by the quick switch shortcut.

## Fullscreen Usage

In Fullscreen mode, open:

`Extensions > Audio Switcher`

Available actions include:

- Switch custom output.
- Use preferred device.
- Select a renamed device.
- Select from all available output devices.
- Set the preferred Fullscreen device.

The `Back + Y` controller shortcut cycles through devices with custom names. Configure at least two renamed devices for this shortcut to work.

## Game-Specific Audio Profiles

Open a game's context menu and go to:

`Audio > Game audio profile`

You can set a preferred output device for that game. When the game starts, the plugin switches to that device. When the game stops, the plugin can restore the previous output device if that option is enabled.

Some games keep using the audio device they opened on startup. If a running game does not move to the new output, switch audio before launching it or restart the game.

## Theme Integration

Theme authors can place Audio Switcher controls anywhere Playnite supports custom plugin elements.

Show the current output device without interaction:

```xml
<ContentControl x:Name="AudioSwitcher_CurrentDevice" />
```

Place a compact quick-switch button that cycles through configured custom devices:

```xml
<ContentControl x:Name="AudioSwitcher_AudioSwitcherButton" />
```

Place a button that opens a compact output selector:

```xml
<ContentControl x:Name="AudioSwitcher_OpenSelectorButton" />
```

Place a full dropdown selector:

```xml
<ContentControl x:Name="AudioSwitcher_AudioDeviceSelector" />
```

Place a controller-friendly device list:

```xml
<ContentControl x:Name="AudioSwitcher_DeviceList" />
```

All controls respect the plugin's display mode setting: text, text plus icon, or icon only. The device selector and device list show the active output marker and use the user's custom names and icons when configured.

## Localization

The plugin uses Playnite localization resource dictionaries under `Localization/`.

Translations are stored as locale-specific XAML resource dictionaries. To add or update a translation, copy an existing locale file, rename it to the target locale, and translate the string values while keeping the same resource keys.

Community translation contributions are welcome.
