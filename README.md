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
- Hide audio devices you do not want to see in Audio Switcher menus.
- Full device selector for manual switching.
- Native volume controls for the current default output and input devices.
- Game-specific output and input audio profiles from the game context menu.
- Optional restore of the previous audio device after closing a game-specific profile.
- Experimental Spatial Sound switching through a user-provided external tool.
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
- **Game profiles**: automatic output/input switching per game and restore behavior after closing a game.
- **Spatial sound**: experimental integration through a user-provided `SoundVolumeView.exe` or `svcl.exe`.
- **Notifications**: informational audio-change notifications.

Devices without a custom name still appear in selectors. Custom names and icons are only used to make the device list easier to read.

Notification settings live in their own **Notifications** tab. Disabling notifications hides normal informational messages such as device, volume, mute, and Spatial Sound changes. Important error messages are still shown.

## Fullscreen Usage

In Fullscreen mode, open:

`Extensions > Audio Switcher`

This opens a simple list of active Windows output devices. The current output device is marked with a check. If you configured a custom name for a device, Audio Switcher shows that friendly name instead of the full Windows device name.

Input devices are available from the `Choose input device` submenu. This can be used for microphones, headset mics, capture cards, or other active Windows recording devices.

The optional `Back + RB` controller shortcut cycles through active output devices.

The native Playnite extension menu is text-only, so custom SVG icons are not shown there. Icons are available to Fullscreen themes through the theme integration API and bundled controls.

Output and input volume controls are exposed to themes through the theme integration API. They are not shown in the native Fullscreen extension menu because Playnite closes that menu after each action, which makes repeated volume changes awkward from a controller.

## Game-Specific Audio Profiles

Open a game's context menu and go to:

`Audio Switcher`

You can set an output device from `Audio Switcher > Choose output device`. The `Choose input device` submenu lets you set a microphone, headset mic, webcam mic, capture card input, or any other active Windows recording device for that game.

If the game does not have a saved profile yet, the current Windows default output and input devices are marked in their respective menus. When the game starts, the plugin switches to the selected devices if game profiles are enabled. When the game stops, the plugin can restore the previous output and input devices if that option is enabled.

Some games keep using the audio device they opened on startup. If a running game does not move to the new output, switch audio before launching it or restart the game.

The same caveat may apply to input devices: some games only read the microphone device during startup or voice chat initialization.

## Experimental Spatial Sound

Audio Switcher can optionally call an external tool to change Windows Spatial Sound for the current default output device. This is intended for advanced users who want to pair game profiles with modes like Windows Sonic or Dolby Atmos.

This feature is disabled by default and Audio Switcher does not bundle third-party executables. To test it:

1. Download either SoundVolumeView or svcl from NirSoft.
2. Open Audio Switcher settings.
3. Enable the experimental Spatial Sound integration.
4. Set the path to `SoundVolumeView.exe` or `svcl.exe`.
5. Use a game's `Audio Switcher > Spatial sound` submenu to choose `Do not change`, `Off`, `Windows Sonic for Headphones`, `Dolby Atmos for Headphones`, or `Dolby Atmos for home theater`.

When a game profile starts, Audio Switcher switches the configured output device first and then applies the selected Spatial Sound mode to the current default render device.

Audio Switcher marks the Spatial Sound mode it last applied during the current Playnite session. Windows does not expose the current Spatial Sound mode through this integration, so changes made outside Audio Switcher may not be reflected in the menu. When Playnite starts and no mode has been applied yet, the menu assumes Spatial Sound is off.

Windows does not currently expose a stable public API for selecting the system Spatial Sound format, so this integration depends on the external tool and should be treated as experimental.

## Theme Integration

Theme authors have two integration paths:

- Use the bundled custom controls for the most reliable controller navigation.
- Use `PluginSettings` bindings for fully custom layouts, where the theme owns focus behavior and visual states.

The native Playnite extension menu can only display text rows. Theme integrations are the recommended way to build a richer console-like selector with icons, custom layout, and gamepad-friendly focus states.

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
- `VolumeStepPercent`
- `Devices`
- `AllDevices`
- `HasDevices`
- `IsSelectorOpen`
- `HighlightedDeviceIndex`

`CurrentDeviceName` is the friendly/custom name for the current output device. `CurrentDeviceIconGeometry` exposes the configured SVG icon geometry, falling back to a speaker icon when needed. `CurrentDeviceLabel` is a convenience label that combines icon text and device name for simple text-based placements.

`CurrentVolume` is a 0.0 to 1.0 scalar value, `CurrentVolumePercent` is the same value as 0 to 100, `CurrentVolumeLabel` is ready for display, and `IsMuted` reports whether the current default output is muted. `CurrentVolume` and `CurrentVolumePercent` can also be written by custom theme controls; writing either value changes the Windows volume.

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

Input devices use the same pattern with input-specific properties and commands:

- `CurrentInputDeviceName`
- `CurrentInputDeviceLabel`
- `CurrentInputDeviceIconGeometry`
- `CurrentInputDeviceId`
- `CurrentInputVolume`
- `CurrentInputVolumePercent`
- `CurrentInputVolumeLabel`
- `IsInputMuted`
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

`AudioSwitcher_InputDeviceList` creates focusable buttons for visible active recording devices. `AudioSwitcher_InputVolumeSlider` changes the current Windows default recording device volume directly and uses the same `VolumeStepPercent` setting for left/right navigation.

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
