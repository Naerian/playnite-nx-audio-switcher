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

`Audio Switcher`

You can set a preferred output device for that game from the list of active playback devices. If the game does not have a saved profile yet, the current system default device is marked in the menu. When the game starts, the plugin switches to the selected device. When the game stops, the plugin can restore the previous output device if that option is enabled.

Some games keep using the audio device they opened on startup. If a running game does not move to the new output, switch audio before launching it or restart the game.

## Theme Integration

Theme authors have two integration paths:

- Use the bundled custom controls for the most reliable controller navigation.
- Use `PluginSettings` bindings for fully custom layouts, where the theme owns focus behavior and visual states.

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
- `PreferredDeviceName`
- `Devices`
- `HasDevices`
- `IsSelectorOpen`
- `HighlightedDeviceIndex`

Useful commands:

- `ToggleSelectorCommand`
- `OpenSelectorCommand`
- `CloseSelectorCommand`
- `NextDeviceCommand`
- `RefreshDevicesCommand`
- `SetDeviceCommand`
- `SetPreferredDeviceCommand`

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

Each item in `Devices` exposes `Id`, `Name`, `WindowsName`, `DisplayName`, `Icon`, `IconGeometry`, `IsCurrent`, `IsPreferred`, `IsHighlighted`, `CurrentMarker`, and `PreferredMarker`.

When `IsSelectorOpen` is true in Fullscreen, Audio Switcher handles gamepad navigation for the exposed selector state: D-pad or left stick up/down changes the highlighted item, `A` selects it, and `B` closes the selector. Themes should style `IsHighlighted` on each `Devices` item so controller users can see the active row even if focus remains outside the custom panel.

Custom controls are still available for simpler placements, but they should be treated as convenience controls rather than the primary Fullscreen integration path.

Show the current output device without interaction:

```xml
<ContentControl x:Name="AudioSwitcher_CurrentDevice" />
```

Place a compact quick-switch button that cycles through configured custom devices:

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

All controls respect the plugin's display mode setting: text, text plus icon, or icon only. The device selector and device list show the active output marker and use the user's custom names and icons when configured.

## Localization

The plugin uses Playnite localization resource dictionaries under `Localization/`.

Translations are stored as locale-specific XAML resource dictionaries. To add or update a translation, copy an existing locale file, rename it to the target locale, and translate the string values while keeping the same resource keys.

Community translation contributions are welcome.

## Support

If you find this project useful and want to support its development, consider buying me a coffee!

[![ko-fi](https://ko-fi.com/img/githubbutton_sm.svg)](https://ko-fi.com/naerian)
