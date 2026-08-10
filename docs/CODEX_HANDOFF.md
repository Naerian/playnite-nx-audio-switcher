# Codex Handoff - Audio Switcher

This file is the operational handoff for future Codex sessions after a PC
format, Playnite reinstall, or Codex CLI reinstall. Start here before making
changes to the extension.

## Project

- Repository: `https://github.com/Naerian/playnite-nx-audio-switcher`
- Local path before format:
  `C:\Users\naria\Documents\Codex\2026-06-07\podriamos-crear-un-plugin-para-playnite\work\PlayniteAudioSwitcher`
- Playnite install used for local testing: `C:\Playnite`
- Installed extension path:
  `C:\Playnite\Extensions\PlayniteAudioSwitcher_708b6ec4-bf96-4c0d-bd9d-fe0aa04d6bf1`
- Wiki clone used during documentation work:
  `C:\Users\naria\Documents\Codex\2026-06-07\podriamos-crear-un-plugin-para-playnite\work\PlayniteAudioSwitcher.wiki`

## Add-on Identity

- AddonId: `PlayniteAudioSwitcher_708b6ec4-bf96-4c0d-bd9d-fe0aa04d6bf1`
- Name: `Audio Switcher`
- Author: `Narian`
- Type for Playnite Addon Database: `Generic`
- Required Playnite SDK/API version: `6.16.0`
- Target framework: `net462`
- Project type: SDK-style WPF project, built with `dotnet build`

## Last Known Stable State

- Branch: `main`
- Last release at the time this file was written: `v1.12.0`
- Last release commit: see tag `v1.12.0`
- Release URL:
  `https://github.com/Naerian/playnite-nx-audio-switcher/releases/tag/v1.12.0`
- Release package:
  `PlayniteAudioSwitcher_708b6ec4-bf96-4c0d-bd9d-fe0aa04d6bf1_1_11_0.pext`
- Public release asset SHA-256:
  `FB94E86D75D7272970F63AFB54EF2230474ED18A15BAF85B53A4C84088983B85`
- Main repo state before this handoff was created: clean and synced with
  `origin/main`.

## What The Extension Does

Audio Switcher controls Windows audio from Playnite Desktop, Fullscreen, game
context menus, and Fullscreen themes.

Current major areas:

- Default output and input device switching.
- Optional preferred output and input devices applied at Playnite startup; game
  profiles can temporarily override them and restore the prior devices.
- Custom device names, Tabler-based icons, visibility, endpoint state, and
  preferred per-device volume.
- Optional device battery and charging-state reporting through generic Windows properties, associated Bluetooth PnP devnodes, and read-only standard HID controls,
  refreshed once per minute and available to Fullscreen themes through API 1.2.0.
- Per-game profiles for output device, input device, Spatial Sound, and game
  session volume.
- Optional per-profile audio process selection for launchers, emulators, and
  games whose Windows audio session cannot be identified automatically.
  The recommended user flow is the game context menu under
  `Audio Switcher > Audio process`, directly below the game-volume submenu; the settings profile manager
  remains available for review and manual changes. Both surfaces support active
  session detection and direct `.exe` selection while the game is closed.
- Optional restore of previous output/input devices after a profiled game
  closes.
- Master output volume, microphone input volume, current game volume, and
  background media session volume controls.
- Media mixer support for apps such as Spotify, browsers, and UniPlaySong
  when they expose normal Windows audio sessions.
- Fullscreen theme integration controls and custom binding API.
- Maintenance tools for backup, restore, diagnostics export, and settings
  stability.
- Settings overview with current devices, volumes, configured profiles,
  Spatial Sound status, and active playback-session count.
- Optional Desktop top-bar battery icon that remains visible without a battery
  value and opens the plugin settings when activated.
- Notification settings split by category.
- Experimental Spatial Sound support through a user-provided SoundVolumeView
  or svcl executable.

## Important Files

- `extension.yaml`: Playnite extension manifest and visible version.
- `installer.yaml`: Playnite installer manifest used by the Addon Database.
- `PlayniteAudioSwitcher.csproj`: build settings and package content list.
- `AudioSwitcherPlugin.cs`: main Playnite extension entrypoint, menus, events,
  theme integration registration, and runtime orchestration.
- `AudioDeviceManager.cs`: Windows Core Audio device/session integration.
- `AudioSwitcherSettings.cs`: saved settings model.
- `AudioSwitcherSettingsView.xaml`: Desktop settings panel.
- `AudioSwitcherThemeApi.cs`: public theme-facing API surface.
- `Examples\FullscreenThemeIntegration.xaml`: commented integration example for
  Fullscreen theme developers.
- `Icons\*.svg`: bundled Tabler icon set used for device icon choices.
- `Icons\LICENSE-TABLER.txt`: bundled Tabler Icons MIT license.
- `media\icon.png`: extension icon used by Playnite and metadata.
- `README.md`: quick public documentation.
- GitHub Wiki: detailed user and theme developer documentation.

## Build

Run from the repository root.

```powershell
dotnet restore .\PlayniteAudioSwitcher.csproj
dotnet clean .\PlayniteAudioSwitcher.csproj -c Release
dotnet build .\PlayniteAudioSwitcher.csproj -c Release --no-restore
```

This repo is SDK-style and should use `dotnet build`. Do not switch it to the
classic MSBuild flow used by some other Playnite plugins unless the project file
changes.

## Package

Use Playnite Toolbox from the local Playnite install.

```powershell
$version = "1.12.0"
$outDir = ".\package-output\v$version"
New-Item -ItemType Directory -Force -Path $outDir | Out-Null
& C:\Playnite\Toolbox.exe pack .\bin\Release $outDir
```

Expected package naming pattern:

```text
PlayniteAudioSwitcher_708b6ec4-bf96-4c0d-bd9d-fe0aa04d6bf1_X_Y_Z.pext
```

Before publishing, inspect the package contents when changing copied assets.
The package should include runtime files such as the manifest, DLL/PDB,
localization dictionaries, icons, media icon, README, and examples. It should
not include local helper folders, `obj`, previous package outputs, or release
verification downloads.

## Install Locally

The user tests against portable Playnite in `C:\Playnite`.

Before copying DLLs or replacing the installed extension, check whether Playnite
is running:

```powershell
Get-Process Playnite.DesktopApp,Playnite.FullscreenApp -ErrorAction SilentlyContinue
```

If Playnite is running, do not overwrite the loaded plugin DLL. Ask the user to
close Playnite or install via `.pext`.

Installed extension folder:

```text
C:\Playnite\Extensions\PlayniteAudioSwitcher_708b6ec4-bf96-4c0d-bd9d-fe0aa04d6bf1
```

For a clean manual install while Playnite is closed, copy the contents of
`bin\Release` into the installed extension folder, preserving subfolders.

## Release Checklist

When the user asks to "sube a GIT y genera release", do the complete flow:

1. Sync version-bearing files:
   - `extension.yaml`
   - `installer.yaml`
   - visible About/version text in code/settings UI
   - any release notes or README references that mention the latest version
2. Build Release.
3. Package with `C:\Playnite\Toolbox.exe pack`.
4. Install into `C:\Playnite` only when Playnite is closed, or use the generated
   `.pext`.
5. Validate:
   - build succeeded
   - package exists
   - package contents look clean
   - `C:\Playnite\Toolbox.exe verify Installer .\installer.yaml` passes
   - local install DLL/package version matches the intended version
6. Commit with a clear release message.
7. Push `main`.
8. Create and push tag `vX.Y.Z`.
9. Create GitHub release and upload the `.pext`.
10. Download or query the public release asset and compare SHA-256 with the local
    package.

Use `gh` if available and authenticated. GitHub raw manifest URLs can lag, so
prefer the GitHub release API or downloading the asset for final verification.

## GitHub Release Commands

Example shape:

```powershell
git status --short --branch
git add extension.yaml installer.yaml README.md <changed files>
git commit -m "Release Audio Switcher X.Y.Z"
git push origin main
git tag vX.Y.Z
git push origin vX.Y.Z
gh release create vX.Y.Z .\package-output\vX.Y.Z\PlayniteAudioSwitcher_708b6ec4-bf96-4c0d-bd9d-fe0aa04d6bf1_X_Y_Z.pext --title "Audio Switcher vX.Y.Z" --notes-file .\release-notes-X.Y.Z.md
```

After publishing:

```powershell
gh release view vX.Y.Z --json tagName,targetCommitish,url,assets,publishedAt
Get-FileHash .\package-output\vX.Y.Z\PlayniteAudioSwitcher_708b6ec4-bf96-4c0d-bd9d-fe0aa04d6bf1_X_Y_Z.pext -Algorithm SHA256
```

If needed, download the public asset into `release-verification\vX.Y.Z` and
compare hashes.

## Playnite Addon Database

The Playnite Addon Database entry should point to this repo's `installer.yaml`.

Known manifest identity:

```yaml
AddonId: PlayniteAudioSwitcher_708b6ec4-bf96-4c0d-bd9d-fe0aa04d6bf1
Type: Generic
Name: Audio Switcher
Author: Narian
InstallerManifestUrl: https://raw.githubusercontent.com/Naerian/playnite-nx-audio-switcher/main/installer.yaml
SourceUrl: https://github.com/Naerian/playnite-nx-audio-switcher
IconUrl: https://raw.githubusercontent.com/Naerian/playnite-nx-audio-switcher/main/media/icon.png
```

For Addon Database updates, use the fork/PR workflow against:

```text
JosefNemec/PlayniteAddonDatabase
```

Only a new entry or metadata update needs a Playnite Addon Database PR. Ordinary
extension releases usually only require updating `installer.yaml` in this repo,
because the database entry points to that installer manifest.

## Theme Integration Notes

Bundled drop-in controls:

```xml
<ContentControl x:Name="AudioSwitcher_DeviceList" />
<ContentControl x:Name="AudioSwitcher_VolumeSlider" />
<ContentControl x:Name="AudioSwitcher_InputDeviceList" />
<ContentControl x:Name="AudioSwitcher_InputVolumeSlider" />
<ContentControl x:Name="AudioSwitcher_GameVolumeSlider" />
<ContentControl x:Name="AudioSwitcher_MediaMixer" />
```

Other useful controls include:

```xml
<ContentControl x:Name="AudioSwitcher_OutputWidget" />
<ContentControl x:Name="AudioSwitcher_BatteryWidget" />
<ContentControl x:Name="AudioSwitcher_InputWidget" />
<ContentControl x:Name="AudioSwitcher_GameVolumeWidget" />
<ContentControl x:Name="AudioSwitcher_MediaSessionList" />
<ContentControl x:Name="AudioSwitcher_MediaVolumeSlider" />
<ContentControl x:Name="AudioSwitcher_MediaWidget" />
```

Custom theme bindings use:

```xml
{PluginSettings Plugin=AudioSwitcher, Path=SomeProperty}
```

The theme API exposes `ApiVersion` and `Supports*` flags. Keep compatibility in
mind when adding or renaming public properties. Avoid breaking existing theme
bindings used by Nexium, Nexus, Aniki Remake, or other community themes.

Important behavior:

- `Devices` and `InputDevices` are active/visible selectors.
- `KnownDevices` and `KnownInputDevices` can include inactive endpoints.
- Media sessions are updated in place where possible to preserve gamepad focus.
- Volume writes are deferred/debounced to reduce slider stutter with gamepad
  navigation.

## Localization

Localization files live in `Localization\*.xaml`.

Current locale files:

- `de_DE`
- `en_US`
- `es_ES`
- `fr_FR`
- `it_IT`
- `ja_JP`
- `ko_KR`
- `pl_PL`
- `pt_BR`
- `ru_RU`
- `tr_TR`
- `zh_CN`

Unsupported locales should fall back to English. When adding a key, keep all
locale dictionaries complete and verify key counts.

## Icons And Licenses

Device icon choices currently use bundled Tabler SVGs from:

```text
C:\Users\naria\Downloads\tabler-icons-main\tabler-icons-main\icons\outline
```

The extension bundles only the selected SVGs under `Icons\` and includes:

```text
Icons\LICENSE-TABLER.txt
```

Lucide assets are no longer used in the current package. Do not re-add Lucide
license files unless Lucide assets are bundled again.

## Spatial Sound

Spatial Sound support is experimental and depends on a user-selected executable:

- SoundVolumeView:
  `https://www.nirsoft.net/utils/sound_volume_view.html`
- svcl:
  `https://www.nirsoft.net/utils/sound_volume_command_line.html`

The extension must not bundle or download these tools automatically. The user
must provide the executable path in settings. The known working Windows Sonic
argument observed during testing used `"Windows Sonic"` as the mode name, while
disabling used an empty mode string.

## Stability Rules

Be defensive around Windows audio state:

- A default output or input device may not exist.
- COM may report `0x80070490` (`Element not found`) when no endpoint is
  available.
- Devices can be disabled, disconnected, not present, or removed while Playnite
  is running.
- Menus and theme API refreshes must not throw unhandled exceptions.
- Game audio sessions may appear after launch and may need retries before the
  per-game volume profile can be applied.
- Media sessions may appear after Playnite and overlays are already open.

Log clearly whether a condition is a real error, a normal missing-device state,
or a dynamic Windows audio state change.

## Data To Preserve Before Formatting

The GitHub repo and releases preserve the source and public packages, but local
user data lives outside the repo.

Recommended before formatting:

- Copy the full portable Playnite folder: `C:\Playnite`
- Export Audio Switcher backup from its settings maintenance tab.
- Keep any diagnostic exports that matter.
- Keep the user-provided SoundVolumeView/svcl executable path or download source.
- Keep custom theme folders that integrate Audio Switcher.

Audio Switcher does not store API keys, but Playnite and other extensions may
store credentials or machine/user-encrypted secrets. Treat those separately.

## How To Resume After Reinstalling Codex

Use this prompt:

```text
Estamos retomando Audio Switcher. Lee docs/CODEX_HANDOFF.md, comprueba el estado del repo y continua desde ahi.
```

Then verify:

```powershell
git status --short --branch
git log -1 --oneline
rg -n "^Id:|^Name:|^Author:|^Version:" extension.yaml
rg -n "Version:|PackageUrl|RequiredApiVersion" installer.yaml
```

If `C:\Playnite` was restored, also verify Toolbox and installed extension:

```powershell
& C:\Playnite\Toolbox.exe verify Installer .\installer.yaml
Get-ChildItem C:\Playnite\Extensions | Where-Object { $_.Name -like "PlayniteAudioSwitcher*" }
```
