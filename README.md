# Playnite NX Audio Switcher

Playnite NX Audio Switcher is a Playnite extension for quickly switching the default Windows audio output device from Desktop mode, Fullscreen mode, game context menus, controller shortcuts, and custom Playnite themes.

It is designed for couch and console-like setups where users often move between TV speakers, headphones, soundbars, wireless headsets, capture devices, or controller audio outputs.

## Features

- Switch the default Windows playback device from Playnite.
- Works from both Desktop and Fullscreen mode extension menus.
- Fullscreen quick switch with `Back + Y`.
- Custom names for any number of audio devices.
- Optional icon per renamed audio device.
- Full device selector for manual switching.
- Game-specific audio profiles from the game context menu.
- Optional restore of the previous audio device after closing a game-specific profile.
- Theme integration controls for theme authors.
- Localizable UI through Playnite resource dictionaries.

## Requirements

- Windows.
- Playnite 10.x.
- Playnite SDK 6.16.0 compatible runtime.

The plugin changes the Windows default playback endpoint using Core Audio / policy configuration APIs.

## Installation

1. Download the latest `.pext` file from the [GitHub releases page](https://github.com/Naerian/playnite-nx-audio-switcher/releases).
2. Open the `.pext` file, or drag it into Playnite.
3. Restart Playnite if Playnite asks you to do so.

After installation, the extension appears as **Audio Switcher** in Playnite's extension menus and settings.

## Configuration

Open:

`Add-ons > Extension settings > Generic > Audio Switcher`

From there you can:

- Give friendly names to audio devices.
- Assign icons to devices.
- Enable or disable the Fullscreen quick switch shortcut.
- Enable or disable automatic game-specific audio profiles.
- Configure whether the previous audio device is restored after a game-specific profile.

Devices without a custom name still appear in selectors. Custom names and icons are only used to make the device list easier to read.

## Fullscreen Usage

In Fullscreen mode, open:

`Extensions > Audio Switcher`

This opens a simple list of active Windows output devices. The current output device is marked with a check. If you configured a custom name for a device, Audio Switcher shows that friendly name instead of the full Windows device name.

The optional `Back + RB` controller shortcut cycles through active output devices.

The native Playnite extension menu is text-only, so custom SVG icons are not shown there. Icons are available to Fullscreen themes through the theme integration API and bundled controls.

## Game-Specific Audio Profiles

Open a game's context menu and go to:

`Audio Switcher`

You can set an output device for that game from the list of active playback devices. If the game does not have a saved profile yet, the current Windows default device is marked in the menu. When the game starts, the plugin switches to the selected device if game profiles are enabled. When the game stops, the plugin can restore the previous output device if that option is enabled.

Some games keep using the audio device they opened on startup. If a running game does not move to the new output, switch audio before launching it or restart the game.

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
- `Devices`
- `HasDevices`
- `IsSelectorOpen`
- `HighlightedDeviceIndex`

`CurrentDeviceName` is the friendly/custom name for the current output device. `CurrentDeviceIconGeometry` exposes the configured SVG icon geometry, falling back to a speaker icon when needed. `CurrentDeviceLabel` is a convenience label that combines icon text and device name for simple text-based placements.

Useful commands:

- `ToggleSelectorCommand`
- `OpenSelectorCommand`
- `CloseSelectorCommand`
- `NextDeviceCommand`
- `RefreshDevicesCommand`
- `SetDeviceCommand`

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

Each item in `Devices` exposes `Id`, `Name`, `WindowsName`, `DisplayName`, `Icon`, `IconGeometry`, `IsCurrent`, `IsHighlighted`, and `CurrentMarker`.

For each device, `Name` is the friendly/custom name when available, `WindowsName` is the original Windows device name, `DisplayName` is ready for a text list and includes the current-device marker, and `IconGeometry` can be used in a `Path`.

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

The device selector and device list show the active output marker and use the user's custom names and icons when configured. Theme authors can decide whether their layouts show text, icons, or both by choosing the exposed properties that fit their design.

## Localization

The plugin uses Playnite localization resource dictionaries under `Localization/`.

Translations are stored as locale-specific XAML resource dictionaries. To add or update a translation, copy an existing locale file, rename it to the target locale, and translate the string values while keeping the same resource keys.

Community translation contributions are welcome.

## Support

If you find this project useful and want to support its development, consider buying me a coffee!

[![ko-fi](https://ko-fi.com/img/githubbutton_sm.svg)](https://ko-fi.com/naerian)
