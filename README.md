# Playnite NX Audio Switcher

Playnite NX Audio Switcher is a Playnite extension for quickly switching Windows audio devices from Desktop mode, Fullscreen mode, game context menus, controller shortcuts, and custom Playnite themes.

It is designed for couch and console-like setups where users often move between TV speakers, headphones, soundbars, wireless headsets, capture devices, or controller audio outputs.

## Features

- Switch the default Windows playback device from Playnite.
- Switch the default Windows recording/input device from Playnite.
- Works from both Desktop and Fullscreen mode extension menus.
- Fullscreen quick switch with `Back + RB`.
- Custom names for any number of output and input audio devices.
- Optional icon per renamed audio device.
- Expanded bundled Lucide icon set for speakers, headsets, microphones, webcams, capture devices, Bluetooth, HDMI, USB and generic audio states.
- Automatic icon suggestions based on Windows device names when no custom icon has been selected.
- Hide audio devices you do not want to see in Audio Switcher menus.
- Full device selector for manual switching.
- Native volume controls for the current default output and input devices.
- Game-specific output, input, Spatial Sound, and game session volume profiles from the game context menu.
- Optional restore of the previous audio device after closing a game-specific profile.
- Experimental Spatial Sound switching through a user-provided external tool.
- Audio session diagnostics export for troubleshooting Windows mixer and game-session detection.
- Settings backup and restore, including device customizations and per-game audio profiles.
- Theme integration controls for theme authors.
- Localizable UI through Playnite resource dictionaries.

## Requirements

- Windows.
- Playnite 10.x.
- Playnite SDK 6.16.0 compatible runtime.

The plugin changes Windows default playback and recording endpoints using Core Audio / policy configuration APIs.

## Installation

1. Download the latest `.pext` file from the [GitHub releases page](https://github.com/Naerian/playnite-nx-audio-switcher/releases).
2. Open the `.pext` file, or drag it into Playnite.
3. Restart Playnite if Playnite asks you to do so.

After installation, the extension appears as **Audio Switcher** in Playnite's extension menus and settings.

## Configuration

Open:

`Add-ons > Extension settings > Generic > Audio Switcher`

The settings window is organized by task:

- **Output**: friendly names, icons, visibility, and default volume for playback devices.
- **Input**: friendly names, icons, visibility, and default input volume for recording devices.
- **Fullscreen**: controller quick switch and the volume step used by Fullscreen theme sliders or theme volume buttons.
- **Game profiles**: automatic output/input switching, Spatial Sound, game session volume, and restore behavior after closing a game.
- **Spatial sound**: experimental integration through a user-provided `SoundVolumeView.exe` or `svcl.exe`.
- **Notifications**: informational notifications by category, including output changes, input changes, volume, mute, game profiles, and Spatial Sound.

Devices without a custom name still appear in selectors. Custom names and icons are only used to make the device list easier to read.

Notification settings live in their own **Notifications** tab. Disabling the master notification option hides normal informational messages, and each category can also be controlled separately. Important error messages are still shown. When a game profile changes multiple things at launch, Audio Switcher groups the result into one profile notification instead of showing one message per device or mode.

## Backup And Diagnostics

In Playnite Desktop mode, open:

`Main menu > Extensions > Audio Switcher`

Available maintenance actions:

- `Tools > Audio session diagnostics`: exports a text report with the playback sessions Windows currently exposes to Audio Switcher, including PID, process name, executable path, session display name, icon path, volume, and mute state. This is useful when a game volume profile or theme game-volume slider cannot find the expected process.
- `Backup and restore > Export settings backup`: exports a JSON backup with Audio Switcher settings and per-game profiles.
- `Backup and restore > Import settings backup`: imports a previously exported JSON backup.

## Fullscreen Usage

In Fullscreen mode, open:

`Extensions > Audio Switcher`

Use `Choose output device` to open the list of active Windows output devices. Use `Choose input device` for microphones, headset mics, capture cards, or other active Windows recording devices.

The current device is marked with a check in each list. If you configured a custom name for a device, Audio Switcher shows that friendly name instead of the full Windows device name.

The optional `Back + RB` controller shortcut cycles through active output devices.

The native Playnite extension menu is text-only, so custom SVG icons are not shown there. Icons are available to Fullscreen themes through the theme integration API and bundled controls.

Output and input volume controls are exposed to themes through the theme integration API. They are not shown in the native Fullscreen extension menu because Playnite closes that menu after each action, which makes repeated volume changes awkward from a controller.

## Game-Specific Audio Profiles

Open a game's context menu and go to:

`Audio Switcher`

You can set an output device from `Audio Switcher > Choose output device`. The `Choose input device` submenu lets you set a microphone, headset mic, webcam mic, capture card input, or any other active Windows recording device for that game.

The `Game volume` submenu lets you optionally choose a launch volume for that specific game. Audio Switcher applies this after Playnite reports the game process as started, then retries briefly while Windows creates the game's audio session.

If the game does not have a saved profile yet, the current Windows default output and input devices are marked in their respective menus. When the game starts, the plugin switches to the selected devices and applies the selected game volume if game profiles are enabled. When the game stops, the plugin can restore the previous output and input devices if that option is enabled.

Use `Audio Switcher > Reset game profile` to remove all saved Audio Switcher settings for that game.

Some games keep using the audio device they opened on startup. If a running game does not move to the new output, switch audio before launching it or restart the game.

The same caveat may apply to input devices: some games only read the microphone device during startup or voice chat initialization.

Game volume control uses the same Windows audio session API that backs the Windows volume mixer. Audio Switcher first follows the process started by Playnite and its child processes, then falls back to running processes inside the game's install directory and newly created mixer sessions after launch. This covers common launcher flows while avoiding changes to unrelated apps.

## Experimental Spatial Sound

Audio Switcher can optionally call an external tool to change Windows Spatial Sound for the current default output device. This is intended for advanced users who want to pair game profiles with modes like Windows Sonic or Dolby Atmos.

This feature is disabled by default and Audio Switcher does not bundle third-party executables. To test it:

1. Download either [SoundVolumeView](https://www.nirsoft.net/utils/sound_volume_view.html) or [svcl](https://www.nirsoft.net/utils/sound_volume_command_line.html) from NirSoft.
2. Open Audio Switcher settings.
3. Enable the experimental Spatial Sound integration.
4. Set the path to `SoundVolumeView.exe` or `svcl.exe`, or use the **Browse** button in the Spatial sound settings tab.
5. Use a game's `Audio Switcher > Spatial sound` submenu to choose `Do not change`, `Off`, `Windows Sonic for Headphones`, `Dolby Atmos for Headphones`, or `Dolby Atmos for home theater`.

`SoundVolumeView.exe` is NirSoft's portable Windows audio utility with a graphical interface and command-line support. `svcl.exe` is NirSoft's console-only version of the same tool and uses the same command syntax.

When a game profile starts, Audio Switcher switches the configured output device first and then applies the selected Spatial Sound mode to the current default render device.

Audio Switcher marks the Spatial Sound mode it last applied during the current Playnite session. Windows does not expose the current Spatial Sound mode through this integration, so changes made outside Audio Switcher may not be reflected in the menu. When Playnite starts and no mode has been applied yet, the menu assumes Spatial Sound is off.

Windows does not currently expose a stable public API for selecting the system Spatial Sound format, so this integration depends on the external tool and should be treated as experimental.

## Theme Integration

Theme authors have two integration paths:

- Use the bundled custom controls for the most reliable controller navigation.
- Use `PluginSettings` bindings for fully custom layouts, where the theme owns focus behavior and visual states.

The native Playnite extension menu can only display text rows. Theme integrations are the recommended way to build a richer console-like selector with icons, custom layout, and gamepad-friendly focus states.

An example XAML file is included in the repository and package at `Examples/FullscreenThemeIntegration.xaml`.

For a quick integration, start with the bundled widgets:

```xml
<ContentControl x:Name="AudioSwitcher_OutputWidget" />
<ContentControl x:Name="AudioSwitcher_InputWidget" />
<ContentControl x:Name="AudioSwitcher_GameVolumeWidget" />
```

These compact controls are intended for topbars and overlays. They expose the current output device, current input device, or current game audio session with icon, label, volume state, and the most common action.

For Fullscreen themes, the safest setup is an icon button plus the bundled `AudioSwitcher_DeviceList` inside a theme panel:

```xml
<ContentControl x:Name="AudioSwitcher_OpenSelectorButton" />

<Border FocusManager.IsFocusScope="True"
        KeyboardNavigation.DirectionalNavigation="Contained"
        KeyboardNavigation.TabNavigation="Cycle">
    <Border.Style>
        <Style TargetType="Border">
            <Setter Property="Visibility" Value="Collapsed" />
            <Style.Triggers>
                <DataTrigger Binding="{PluginSettings Plugin=AudioSwitcher, Path=IsSelectorOpen}" Value="True">
                    <Setter Property="Visibility" Value="Visible" />
                </DataTrigger>
            </Style.Triggers>
        </Style>
    </Border.Style>

    <ContentControl x:Name="AudioSwitcher_DeviceList" />
</Border>
```

The `AudioSwitcher_DeviceList` control creates real focusable buttons for all active playback devices, registers itself with the plugin, and receives focus when `OpenSelectorCommand` or `ToggleSelectorCommand` opens the selector. Pressing `A` selects the focused device button and closes the panel. This is the recommended option for console-like gamepad navigation.

Themes that build their own panel should make it a focus scope and contain directional navigation:

```xml
<Grid FocusManager.IsFocusScope="True"
      KeyboardNavigation.DirectionalNavigation="Contained"
      KeyboardNavigation.TabNavigation="Cycle">
    <!-- Custom selector content -->
</Grid>
```

Available binding source:

```xml
{PluginSettings Plugin=AudioSwitcher, Path=CurrentDeviceName}
```

Useful properties:

- `CurrentDeviceName`
- `CurrentDeviceLabel`
- `CurrentDeviceIconGeometry`
- `CurrentDeviceId`
- `CurrentVolume`
- `CurrentVolumePercent`
- `CurrentVolumeLabel`
- `IsMuted`
- `IsOutputMuted`
- `CurrentOutputVolumeIconGeometry`
- `CurrentGameName`
- `CurrentGameProcessName`
- `CurrentGameProcessPath`
- `CurrentGameSessionName`
- `CurrentGameSessionIconPath`
- `GameSessionStatusLabel`
- `CurrentGameVolume`
- `CurrentGameVolumePercent`
- `CurrentGameVolumeLabel`
- `IsGameMuted`
- `CurrentGameVolumeIconGeometry`
- `HasActiveGameAudioSession`
- `LastChangeType`
- `LastChangeMessage`
- `LastChangeAt`
- `LastChangeIconGeometry`
- `VolumeStepPercent`
- `Devices`
- `AllDevices`
- `HasDevices`
- `IsSelectorOpen`
- `HighlightedDeviceIndex`

`CurrentDeviceName` is the friendly/custom name for the current output device. `CurrentDeviceIconGeometry` exposes the configured SVG icon geometry, falling back to a speaker icon when needed. `CurrentDeviceLabel` is a convenience label that combines icon text and device name for simple text-based placements.

`CurrentVolume` is a 0.0 to 1.0 scalar value, `CurrentVolumePercent` is the same value as 0 to 100, `CurrentVolumeLabel` is ready for display, and `IsMuted` reports whether the current default output is muted. `CurrentVolume` and `CurrentVolumePercent` can also be written by custom theme controls; writing either value changes the Windows volume.

`IsOutputMuted` is an alias for output mute state, useful when a theme wants symmetric output/input/game names. `CurrentOutputVolumeIconGeometry`, `CurrentInputVolumeIconGeometry`, and `CurrentGameVolumeIconGeometry` expose ready-to-render icon geometry for mute and volume state.

`CurrentGameVolume` and `CurrentGameVolumePercent` work the same way, but target the audio session of the game currently launched by Playnite. `HasActiveGameAudioSession` is `false` when no game is running or Windows has not created a matching game audio session yet. This is useful for overlays that should hide or disable a game-volume slider until it can actually control something.

`CurrentGameProcessName`, `CurrentGameProcessPath`, `CurrentGameSessionName`, and `CurrentGameSessionIconPath` expose the Windows audio session currently matched by Audio Switcher for the running game. Theme authors can use these values for debugging overlays or richer game-audio widgets. Cover art and game metadata should still usually come from Playnite/theme data, because Audio Switcher only exposes the Windows audio session it controls.

`GameSessionStatusLabel` is a localized, display-ready status string for the current game session, such as no game running, waiting for a game audio session, or controlling a specific process/session.

`LastChangeType`, `LastChangeMessage`, `LastChangeAt`, and `LastChangeIconGeometry` let themes build their own in-theme toast or notification area when Audio Switcher changes output, input, volume, mute, or game-session volume.

`VolumeStepPercent` is writable and controls how many percentage points the volume changes when a Fullscreen theme uses left/right on `AudioSwitcher_VolumeSlider`, `VolumeUpCommand`, or `VolumeDownCommand`. Desktop volume actions use the same step.

Useful commands:

- `ToggleSelectorCommand`
- `OpenSelectorCommand`
- `CloseSelectorCommand`
- `NextDeviceCommand`
- `RefreshDevicesCommand`
- `SetDeviceCommand`
- `VolumeUpCommand`
- `VolumeDownCommand`
- `SetVolumeCommand`
- `ToggleMuteCommand`
- `RefreshVolumeCommand`
- `GameVolumeUpCommand`
- `GameVolumeDownCommand`
- `SetGameVolumeCommand`
- `ToggleGameMuteCommand`
- `RefreshGameVolumeCommand`

Minimal topbar button:

```xml
<StackPanel Orientation="Horizontal">
    <ContentControl x:Name="AudioSwitcher_OpenSelectorButton" />
    <TextBlock Text="{PluginSettings Plugin=AudioSwitcher, Path=CurrentDeviceName}" />
</StackPanel>
```

Minimal overlay panel:

```xml
<StackPanel>
    <ContentControl x:Name="AudioSwitcher_OutputWidget" />
    <ContentControl x:Name="AudioSwitcher_DeviceList" />
    <ContentControl x:Name="AudioSwitcher_VolumeSlider" />
    <ContentControl x:Name="AudioSwitcher_InputWidget" />
    <ContentControl x:Name="AudioSwitcher_InputVolumeSlider" />
    <ContentControl x:Name="AudioSwitcher_GameVolumeWidget" />
    <ContentControl x:Name="AudioSwitcher_GameVolumeSlider" />
</StackPanel>
```

Minimal in-theme change toast:

```xml
<StackPanel Orientation="Horizontal">
    <Path Data="{PluginSettings Plugin=AudioSwitcher, Path=LastChangeIconGeometry}" />
    <TextBlock Text="{PluginSettings Plugin=AudioSwitcher, Path=LastChangeMessage}" />
</StackPanel>
```

Example icon button:

```xml
<Button Command="{PluginSettings Plugin=AudioSwitcher, Path=ToggleSelectorCommand}">
    <Path Data="{PluginSettings Plugin=AudioSwitcher, Path=CurrentDeviceIconGeometry}" />
</Button>
```

Example device list:

```xml
<ItemsControl ItemsSource="{PluginSettings Plugin=AudioSwitcher, Path=Devices}">
    <ItemsControl.ItemTemplate>
        <DataTemplate>
            <Button Command="{PluginSettings Plugin=AudioSwitcher, Path=SetDeviceCommand}"
                    CommandParameter="{Binding Id}">
                <TextBlock Text="{Binding DisplayName}" />
            </Button>
        </DataTemplate>
    </ItemsControl.ItemTemplate>
</ItemsControl>
```

Recommended bundled volume slider:

```xml
<ContentControl x:Name="AudioSwitcher_VolumeSlider" />
```

`AudioSwitcher_VolumeSlider` creates a real focusable slider, updates the current Windows default output volume directly, supports left/right keyboard or gamepad navigation, and uses the volume step configured in Audio Switcher settings.

Example custom volume controls:

```xml
<StackPanel Orientation="Horizontal">
    <Button Command="{PluginSettings Plugin=AudioSwitcher, Path=VolumeDownCommand}"
            Content="-" />

    <ProgressBar Minimum="0"
                 Maximum="100"
                 Width="120"
                 Value="{PluginSettings Plugin=AudioSwitcher, Path=CurrentVolumePercent}" />

    <Button Command="{PluginSettings Plugin=AudioSwitcher, Path=VolumeUpCommand}"
            Content="+" />

    <Button Command="{PluginSettings Plugin=AudioSwitcher, Path=ToggleMuteCommand}"
            Content="{PluginSettings Plugin=AudioSwitcher, Path=CurrentVolumeLabel}" />
</StackPanel>
```

`SetVolumeCommand` accepts either a scalar value such as `0.5` or a percentage value such as `50` / `50%`.

Theme authors who prefer their own slider can bind to `CurrentVolumePercent` with a two-way binding:

```xml
<Slider Minimum="0"
        Maximum="100"
        Value="{PluginSettings Plugin=AudioSwitcher, Path=CurrentVolumePercent, Mode=TwoWay}" />
```

Recommended bundled current game volume slider:

```xml
<ContentControl x:Name="AudioSwitcher_GameVolumeSlider" />
```

`AudioSwitcher_GameVolumeSlider` creates a real focusable slider for the running game's Windows audio session, supports left/right keyboard or gamepad navigation, uses `VolumeStepPercent`, and disables itself when no matching game audio session is available.

Custom current game volume slider:

```xml
<Slider Minimum="0"
        Maximum="100"
        IsEnabled="{PluginSettings Plugin=AudioSwitcher, Path=HasActiveGameAudioSession}"
        Value="{PluginSettings Plugin=AudioSwitcher, Path=CurrentGameVolumePercent, Mode=TwoWay}" />
```

Input devices use the same pattern with input-specific properties and commands:

- `CurrentInputDeviceName`
- `CurrentInputDeviceLabel`
- `CurrentInputDeviceIconGeometry`
- `CurrentInputDeviceId`
- `CurrentInputVolume`
- `CurrentInputVolumePercent`
- `CurrentInputVolumeLabel`
- `IsInputMuted`
- `CurrentInputVolumeIconGeometry`
- `InputDevices`
- `AllInputDevices`
- `HasInputDevices`

Useful input commands:

- `RefreshInputDevicesCommand`
- `SetInputDeviceCommand`
- `InputVolumeUpCommand`
- `InputVolumeDownCommand`
- `SetInputVolumeCommand`
- `ToggleInputMuteCommand`
- `RefreshInputVolumeCommand`

Recommended bundled input controls:

```xml
<ContentControl x:Name="AudioSwitcher_InputDeviceList" />
<ContentControl x:Name="AudioSwitcher_InputVolumeSlider" />
```

`AudioSwitcher_InputDeviceList` creates focusable buttons for visible active recording devices. `AudioSwitcher_InputVolumeSlider` creates a real focusable microphone/input volume slider, changes the current Windows default recording device volume directly, supports left/right keyboard or gamepad navigation, and uses the same `VolumeStepPercent` setting as the output volume slider.

Custom input device list:

```xml
<ItemsControl ItemsSource="{PluginSettings Plugin=AudioSwitcher, Path=InputDevices}">
    <ItemsControl.ItemTemplate>
        <DataTemplate>
            <Button Command="{PluginSettings Plugin=AudioSwitcher, Path=SetInputDeviceCommand}"
                    CommandParameter="{Binding Id}">
                <TextBlock Text="{Binding DisplayName}" />
            </Button>
        </DataTemplate>
    </ItemsControl.ItemTemplate>
</ItemsControl>
```

Custom input volume slider:

```xml
<Slider Minimum="0"
        Maximum="100"
        Value="{PluginSettings Plugin=AudioSwitcher, Path=CurrentInputVolumePercent, Mode=TwoWay}" />
```

Themes can also expose the volume step in their own settings UI:

```xml
<Slider Minimum="1"
        Maximum="50"
        IsSnapToTickEnabled="True"
        TickFrequency="1"
        Value="{PluginSettings Plugin=AudioSwitcher, Path=VolumeStepPercent, Mode=TwoWay}" />
```

Each item in `Devices`, `AllDevices`, `InputDevices`, and `AllInputDevices` exposes `Id`, `Name`, `WindowsName`, `DisplayName`, `Icon`, `IconGeometry`, `IsVisible`, `IsCurrent`, `IsHighlighted`, and `CurrentMarker`.

For each device, `Name` is the friendly/custom name when available, `WindowsName` is the original Windows device name, `DisplayName` is ready for a text list and includes the current-device marker, and `IconGeometry` can be used in a `Path`.

`Devices` and `InputDevices` contain only devices that the user left visible in Audio Switcher settings. `AllDevices` and `AllInputDevices` contain active devices including those hidden from normal extension menus, so advanced themes can build their own management UI if needed.

When `IsSelectorOpen` is true in Fullscreen, Audio Switcher handles gamepad navigation for the exposed selector state: D-pad or left stick up/down changes the highlighted item, `A` selects it, and `B` closes the selector. Themes should style `IsHighlighted` on each `Devices` item so controller users can see the active row even if focus remains outside the custom panel.

Custom controls are still available for simpler placements, but they should be treated as convenience controls rather than the primary Fullscreen integration path.

Show the current output device without interaction:

```xml
<ContentControl x:Name="AudioSwitcher_CurrentDevice" />
```

Place a compact quick-switch button that cycles through active output devices:

```xml
<ContentControl x:Name="AudioSwitcher_AudioSwitcherButton" />
```

Place an icon button that toggles `IsSelectorOpen`:

```xml
<ContentControl x:Name="AudioSwitcher_OpenSelectorButton" />
```

This control renders as an icon-only button and toggles the exposed selector state. The theme should render the actual selector panel using the `Devices` collection and commands above. The icon uses the current device icon when configured, and falls back to the bundled speaker icon. Theme authors can override the fallback icon by defining an `AudioSwitcher_DefaultIconGeometry` geometry resource.

Place a full dropdown selector:

```xml
<ContentControl x:Name="AudioSwitcher_AudioDeviceSelector" />
```

Place a controller-friendly device list:

```xml
<ContentControl x:Name="AudioSwitcher_DeviceList" />
```

Place a controller-friendly volume slider:

```xml
<ContentControl x:Name="AudioSwitcher_VolumeSlider" />
```

Place a controller-friendly current game volume slider:

```xml
<ContentControl x:Name="AudioSwitcher_GameVolumeSlider" />
```

Place a controller-friendly input device list and input volume slider:

```xml
<ContentControl x:Name="AudioSwitcher_InputDeviceList" />
<ContentControl x:Name="AudioSwitcher_InputVolumeSlider" />
```

The device selector and device lists show the active marker and use the user's custom names and icons when configured. Theme authors can decide whether their layouts show text, icons, or both by choosing the exposed properties that fit their design.

## Localization

The plugin uses Playnite localization resource dictionaries under `Localization/`.

Translations are stored as locale-specific XAML resource dictionaries. To add or update a translation, copy an existing locale file, rename it to the target locale, and translate the string values while keeping the same resource keys.

Community translation contributions are welcome.

## Support

If you find this project useful and want to support its development, consider buying me a coffee!

[![ko-fi](https://ko-fi.com/img/githubbutton_sm.svg)](https://ko-fi.com/naerian)
