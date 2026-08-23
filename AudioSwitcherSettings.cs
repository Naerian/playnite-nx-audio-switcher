using System.Collections.Generic;
using System.Linq;
using Playnite.SDK;
using Playnite.SDK.Data;

namespace PlayniteAudioSwitcher
{
    public sealed class AudioSwitcherSettings : ObservableObject, ISettings
    {
        private readonly AudioSwitcherPlugin plugin;
        private AudioSwitcherSettings editingClone;
        private List<AudioDevice> availablePlaybackDevices = new List<AudioDevice>();
        private List<AudioDevice> availableRecordingDevices = new List<AudioDevice>();
        private List<AudioDevice> preferredPlaybackDeviceOptions = new List<AudioDevice>();
        private List<AudioDevice> preferredRecordingDeviceOptions = new List<AudioDevice>();
        private List<GameAudioProfileEntry> availableGameProfiles = new List<GameAudioProfileEntry>();
        private List<AudioDeviceAlias> deviceAliases = new List<AudioDeviceAlias>();
        private List<AudioDeviceAlias> inputDeviceAliases = new List<AudioDeviceAlias>();
        private string favoriteDeviceAId;
        private string favoriteDeviceAName;
        private string favoriteDeviceBId;
        private string favoriteDeviceBName;
        private string preferredOutputDeviceId = string.Empty;
        private string preferredInputDeviceId = string.Empty;
        private string deviceDisplayMode = "TextAndIcon";
        private bool showNotifications = true;
        private bool showOutputDeviceNotifications = true;
        private bool showInputDeviceNotifications = true;
        private bool showVolumeNotifications;
        private bool showMuteNotifications;
        private bool showGameProfileNotifications = true;
        private bool showSpatialSoundNotifications = true;
        private bool showDiagnosticNotifications = true;
        private bool quickSwitchEnabled;
        private bool quickSwitchAllDevices = true;
        private bool showMediaSessionIcons = true;
        private bool gameProfilesEnabled = true;
        private bool restoreDeviceAfterGameProfile = true;
        private bool spatialSoundIntegrationEnabled;
        private string spatialSoundToolPath;
        private string currentSpatialSoundMode;
        private int volumeStepPercent = 2;
        private bool showDesktopBatteryIndicator;
        private bool colorDesktopIndicatorByBattery = true;
        private string desktopTopPanelIcon;
        private string desktopBatteryDisplayMode;
        private bool hideBatteryIndicatorWhenUnavailable = true;
        private bool showDisabledOutputDevices;
        private bool showDisabledInputDevices;
        private string batteryIndicatorDisplayMode = "IconAndPercentage";
        private string batteryIndicatorIcon = string.Empty;
        private string appearancePreset = SettingsAppearance.Midnight;
        private bool setupWizardCompleted;
        private int settingsSchemaVersion;

        public const int CurrentSettingsSchemaVersion = 1;

        public AudioSwitcherSettings()
        {
        }

        public AudioSwitcherSettings(AudioSwitcherPlugin plugin)
        {
            this.plugin = plugin;
            var savedSettings = plugin.LoadPluginSettings<AudioSwitcherSettings>();
            if (savedSettings != null)
            {
                FavoriteDeviceAId = savedSettings.FavoriteDeviceAId;
                FavoriteDeviceAName = savedSettings.FavoriteDeviceAName;
                FavoriteDeviceBId = savedSettings.FavoriteDeviceBId;
                FavoriteDeviceBName = savedSettings.FavoriteDeviceBName;
                DeviceAliases = savedSettings.DeviceAliases ?? new List<AudioDeviceAlias>();
                InputDeviceAliases = savedSettings.InputDeviceAliases ?? new List<AudioDeviceAlias>();
                PreferredOutputDeviceId = savedSettings.PreferredOutputDeviceId;
                PreferredInputDeviceId = savedSettings.PreferredInputDeviceId;
                DeviceDisplayMode = string.IsNullOrWhiteSpace(savedSettings.DeviceDisplayMode) ? "TextAndIcon" : savedSettings.DeviceDisplayMode;
                ShowNotifications = savedSettings.ShowNotifications;
                QuickSwitchEnabled = savedSettings.QuickSwitchEnabled;
                QuickSwitchAllDevices = savedSettings.QuickSwitchAllDevices;
                ShowMediaSessionIcons = savedSettings.ShowMediaSessionIcons;
                GameProfilesEnabled = savedSettings.GameProfilesEnabled;
                RestoreDeviceAfterGameProfile = savedSettings.RestoreDeviceAfterGameProfile;
                SpatialSoundIntegrationEnabled = savedSettings.SpatialSoundIntegrationEnabled;
                SpatialSoundToolPath = savedSettings.SpatialSoundToolPath;
                ShowOutputDeviceNotifications = savedSettings.ShowOutputDeviceNotifications;
                ShowInputDeviceNotifications = savedSettings.ShowInputDeviceNotifications;
                ShowVolumeNotifications = savedSettings.ShowVolumeNotifications;
                ShowMuteNotifications = savedSettings.ShowMuteNotifications;
                ShowGameProfileNotifications = savedSettings.ShowGameProfileNotifications;
                ShowSpatialSoundNotifications = savedSettings.ShowSpatialSoundNotifications;
                ShowDiagnosticNotifications = savedSettings.ShowDiagnosticNotifications;
                VolumeStepPercent = savedSettings.VolumeStepPercent <= 0 ? 2 : savedSettings.VolumeStepPercent;
                ShowDesktopBatteryIndicator = savedSettings.ShowDesktopBatteryIndicator;
                ColorDesktopIndicatorByBattery = savedSettings.ColorDesktopIndicatorByBattery;
                DesktopTopPanelIcon = savedSettings.DesktopTopPanelIcon ?? savedSettings.BatteryIndicatorIcon ?? string.Empty;
                DesktopBatteryDisplayMode = savedSettings.DesktopBatteryDisplayMode ??
                    MigrateDesktopBatteryDisplayMode(savedSettings.BatteryIndicatorDisplayMode);
                HideBatteryIndicatorWhenUnavailable = savedSettings.HideBatteryIndicatorWhenUnavailable;
                ShowDisabledOutputDevices = savedSettings.ShowDisabledOutputDevices;
                ShowDisabledInputDevices = savedSettings.ShowDisabledInputDevices;
                BatteryIndicatorDisplayMode = string.IsNullOrWhiteSpace(savedSettings.BatteryIndicatorDisplayMode)
                    ? "IconAndPercentage"
                    : savedSettings.BatteryIndicatorDisplayMode;
                BatteryIndicatorIcon = savedSettings.BatteryIndicatorIcon ?? string.Empty;
                AppearancePreset = savedSettings.AppearancePreset;
                SetupWizardCompleted = savedSettings.SetupWizardCompleted;
                SettingsSchemaVersion = savedSettings.SettingsSchemaVersion;
            }

            DesktopTopPanelIcon = DesktopTopPanelIcon ?? string.Empty;
            BatteryIndicatorIcon = BatteryIndicatorIcon ?? string.Empty;
            DesktopTopPanelIcon = ResolveIconId(DesktopTopPanelIcon);
            BatteryIndicatorIcon = ResolveIconId(BatteryIndicatorIcon);
            DesktopBatteryDisplayMode = string.IsNullOrWhiteSpace(DesktopBatteryDisplayMode)
                ? "IconAndPercentage"
                : DesktopBatteryDisplayMode;
            AppearancePreset = SettingsAppearance.Normalize(AppearancePreset);

            MigrateSettings(savedSettings != null);
            MigrateFavoritesToAliases();
            ClearLegacyFavoriteSettings();
            RefreshDevices();
        }

        [DontSerialize]
        internal AudioSwitcherPlugin Plugin => plugin;

        public string FavoriteDeviceAId
        {
            get => favoriteDeviceAId;
            set => SetValue(ref favoriteDeviceAId, value);
        }

        public string FavoriteDeviceAName
        {
            get => favoriteDeviceAName;
            set => SetValue(ref favoriteDeviceAName, value);
        }

        public string FavoriteDeviceBId
        {
            get => favoriteDeviceBId;
            set => SetValue(ref favoriteDeviceBId, value);
        }

        public string FavoriteDeviceBName
        {
            get => favoriteDeviceBName;
            set => SetValue(ref favoriteDeviceBName, value);
        }

        public List<AudioDeviceAlias> DeviceAliases
        {
            get => deviceAliases;
            set => SetValue(ref deviceAliases, value ?? new List<AudioDeviceAlias>());
        }

        public List<AudioDeviceAlias> InputDeviceAliases
        {
            get => inputDeviceAliases;
            set => SetValue(ref inputDeviceAliases, value ?? new List<AudioDeviceAlias>());
        }

        [DontSerialize]
        internal bool IsEditing => editingClone != null;

        public string PreferredOutputDeviceId
        {
            get => preferredOutputDeviceId;
            set => SetValue(ref preferredOutputDeviceId, NormalizePreferredDeviceId(value));
        }

        public string PreferredInputDeviceId
        {
            get => preferredInputDeviceId;
            set => SetValue(ref preferredInputDeviceId, NormalizePreferredDeviceId(value));
        }

        private static string NormalizePreferredDeviceId(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value;
        }

        public string DeviceDisplayMode
        {
            get => deviceDisplayMode;
            set => SetValue(ref deviceDisplayMode, value);
        }

        [DontSerialize]
        private List<string> LegacyIconOptions => new List<string>
        {
            string.Empty,
            "🔊",
            "🔈",
            "🎧",
            "📺",
            "🖥",
            "🎮",
            "🔵",
            "⭐"
        };

        [DontSerialize]
        public List<AudioIconOption> IconOptions => new List<AudioIconOption>
        {
            new AudioIconOption { Id = string.Empty, Name = plugin?.Loc("LOCAS_NoIcon") ?? "None", Glyph = string.Empty },
            IconOption("volume-2", "Volume", "V+", "volume.svg"),
            IconOption("volume-1", "Volume low", "V", "volume-2.svg"),
            IconOption("volume", "Volume off", "VOL", "volume-4.svg"),
            IconOption("volume-off", "Volume muted", "MUTE", "volume-off.svg"),
            IconOption("volume-x", "Volume unavailable", "VX", "volume-3.svg"),
            IconOption("headphones", "Headphones", "HP", "headphones.svg"),
            IconOption("headphones-off", "Headphones unavailable", "HP-", "headphones-off.svg"),
            IconOption("headset", "Headset", "HS", "headset.svg"),
            IconOption("headset-off", "Headset unavailable", "HS-", "headset-off.svg"),
            IconOption("device-airpods", "Wireless earbuds", "BUD", "device-airpods.svg"),
            IconOption("earphone-bluetooth", "Bluetooth earphone", "BTE", "earphone-bluetooth.svg"),
            IconOption("mic", "Microphone", "MIC", "microphone.svg"),
            IconOption("mic-off", "Microphone off", "MIC-", "microphone-off.svg"),
            IconOption("mic-vocal", "Vocal microphone", "VOC", "microphone-2.svg"),
            IconOption("mic-vocal-off", "Vocal microphone off", "VOC-", "microphone-2-off.svg"),
            IconOption("webcam", "Webcam", "CAM", "device-computer-camera.svg"),
            IconOption("webcam-off", "Webcam unavailable", "CAM-", "device-computer-camera-off.svg"),
            IconOption("capture", "Capture device", "CAP", "capture.svg"),
            IconOption("audio-lines", "Audio controls", "EQ", "adjustments-horizontal.svg"),
            IconOption("audio-waveform", "Waveform", "WAV", "wave-sine.svg"),
            IconOption("music", "Music", "MUS", "music.svg"),
            IconOption("podcast", "Podcast", "POD", "broadcast.svg"),
            IconOption("radio", "Radio", "RAD", "radio.svg"),
            IconOption("boom-box", "Audio player", "BOX", "device-audio-tape.svg"),
            IconOption("vinyl", "Turntable", "VIN", "vinyl.svg"),
            IconOption("speaker", "Speaker", "SP", "device-speaker.svg"),
            IconOption("speaker-off", "Speaker unavailable", "SP-", "device-speaker-off.svg"),
            IconOption("speakerphone", "Speakerphone", "CALL", "speakerphone.svg"),
            IconOption("tv", "TV", "TV", "device-tv.svg"),
            IconOption("monitor", "Monitor", "PC", "device-desktop.svg"),
            IconOption("laptop", "Laptop", "LAP", "device-laptop.svg"),
            IconOption("bluetooth", "Bluetooth", "BT", "bluetooth.svg"),
            IconOption("bluetooth-connected", "Bluetooth connected", "BT+", "bluetooth-connected.svg"),
            IconOption("bluetooth-searching", "Bluetooth unavailable", "BT-", "bluetooth-off.svg"),
            IconOption("usb", "USB", "USB", "usb.svg"),
            IconOption("hdmi-port", "HDMI", "HDMI", "plug.svg"),
            IconOption("cable", "Cable", "CAB", "plug-connected.svg"),
            IconOption("cast", "Cast device", "CAST", "cast.svg")
        };

        public const string DefaultVolumeIconId = "volume-2";

        [DontSerialize]
        public List<AudioIconOption> BatteryIconOptions
        {
            get
            {
                var options = IconOptions;
                if (options.Count > 0)
                {
                    options[0].Name = plugin?.Loc("LOCAS_DeviceIcon") ?? "Device icon";
                    options[0].IconFileName = "volume.svg";
                }

                return options;
            }
        }

        private static AudioIconOption IconOption(string id, string name, string glyph, string iconFileName)
        {
            return new AudioIconOption
            {
                Id = id,
                Name = name,
                Glyph = glyph,
                IconFileName = iconFileName
            };
        }

        public bool ShowNotifications
        {
            get => showNotifications;
            set => SetValue(ref showNotifications, value);
        }

        public bool ShowOutputDeviceNotifications
        {
            get => showOutputDeviceNotifications;
            set => SetValue(ref showOutputDeviceNotifications, value);
        }

        public bool ShowInputDeviceNotifications
        {
            get => showInputDeviceNotifications;
            set => SetValue(ref showInputDeviceNotifications, value);
        }

        public bool ShowVolumeNotifications
        {
            get => showVolumeNotifications;
            set => SetValue(ref showVolumeNotifications, value);
        }

        public bool ShowMuteNotifications
        {
            get => showMuteNotifications;
            set => SetValue(ref showMuteNotifications, value);
        }

        public bool ShowGameProfileNotifications
        {
            get => showGameProfileNotifications;
            set => SetValue(ref showGameProfileNotifications, value);
        }

        public bool ShowSpatialSoundNotifications
        {
            get => showSpatialSoundNotifications;
            set => SetValue(ref showSpatialSoundNotifications, value);
        }

        public bool ShowDiagnosticNotifications
        {
            get => showDiagnosticNotifications;
            set => SetValue(ref showDiagnosticNotifications, value);
        }

        public bool QuickSwitchEnabled
        {
            get => quickSwitchEnabled;
            set => SetValue(ref quickSwitchEnabled, value);
        }

        public bool QuickSwitchAllDevices
        {
            get => quickSwitchAllDevices;
            set => SetValue(ref quickSwitchAllDevices, value);
        }

        public bool ShowMediaSessionIcons
        {
            get => showMediaSessionIcons;
            set => SetValue(ref showMediaSessionIcons, value);
        }

        public bool GameProfilesEnabled
        {
            get => gameProfilesEnabled;
            set => SetValue(ref gameProfilesEnabled, value);
        }

        public bool RestoreDeviceAfterGameProfile
        {
            get => restoreDeviceAfterGameProfile;
            set => SetValue(ref restoreDeviceAfterGameProfile, value);
        }

        public bool SpatialSoundIntegrationEnabled
        {
            get => spatialSoundIntegrationEnabled;
            set => SetValue(ref spatialSoundIntegrationEnabled, value);
        }

        public int VolumeStepPercent
        {
            get => volumeStepPercent;
            set => SetValue(ref volumeStepPercent, System.Math.Max(1, System.Math.Min(50, value)));
        }

        public string SpatialSoundToolPath
        {
            get => spatialSoundToolPath;
            set => SetValue(ref spatialSoundToolPath, value);
        }

        [DontSerialize]
        public string CurrentSpatialSoundMode
        {
            get => currentSpatialSoundMode;
            set => SetValue(ref currentSpatialSoundMode, value);
        }

        [DontSerialize]
        public List<SpatialSoundModeOption> SpatialSoundModeOptions => new List<SpatialSoundModeOption>
        {
            new SpatialSoundModeOption { Id = string.Empty, Name = plugin?.Loc("LOCAS_SpatialDoNotChange") ?? "Do not change" },
            new SpatialSoundModeOption { Id = "Off", Name = plugin?.Loc("LOCAS_SpatialOff") ?? "Off", ToolValue = string.Empty },
            new SpatialSoundModeOption { Id = "WindowsSonicHeadphones", Name = "Windows Sonic for Headphones", ToolValue = "Windows Sonic" },
            new SpatialSoundModeOption { Id = "DolbyAtmosHeadphones", Name = "Dolby Atmos for Headphones", ToolValue = "Dolby Atmos for Headphones" },
            new SpatialSoundModeOption { Id = "DolbyAtmosHomeTheater", Name = "Dolby Atmos for home theater", ToolValue = "Dolby Atmos for home theater" }
        };

        [DontSerialize]
        public List<AudioDevice> AvailablePlaybackDevices
        {
            get => availablePlaybackDevices;
            set => SetValue(ref availablePlaybackDevices, value);
        }

        [DontSerialize]
        public List<AudioDevice> AvailableRecordingDevices
        {
            get => availableRecordingDevices;
            set => SetValue(ref availableRecordingDevices, value);
        }

        [DontSerialize]
        public List<AudioDevice> PreferredPlaybackDeviceOptions
        {
            get => preferredPlaybackDeviceOptions;
            private set => SetValue(ref preferredPlaybackDeviceOptions, value);
        }

        [DontSerialize]
        public List<AudioDevice> PreferredRecordingDeviceOptions
        {
            get => preferredRecordingDeviceOptions;
            private set => SetValue(ref preferredRecordingDeviceOptions, value);
        }

        [DontSerialize]
        public List<GameAudioProfileEntry> AvailableGameProfiles
        {
            get => availableGameProfiles;
            set => SetValue(ref availableGameProfiles, value ?? new List<GameAudioProfileEntry>());
        }

        public void RefreshDevices()
        {
            // Replacing a ComboBox ItemsSource can briefly clear SelectedValue and push
            // that transient value back through a two-way binding. Keep the persisted
            // IDs locally so a background device/battery refresh cannot erase them.
            var selectedOutputDeviceId = PreferredOutputDeviceId;
            var selectedInputDeviceId = PreferredInputDeviceId;
            AvailablePlaybackDevices = RefreshDeviceList(DeviceAliases, () => plugin.AudioDevices.GetAllPlaybackDevices(), false);
            AvailableRecordingDevices = RefreshDeviceList(InputDeviceAliases, () => plugin.AudioDevices.GetAllRecordingDevices(), true);
            PreferredPlaybackDeviceOptions = CreatePreferredDeviceOptions(AvailablePlaybackDevices, selectedOutputDeviceId);
            PreferredRecordingDeviceOptions = CreatePreferredDeviceOptions(AvailableRecordingDevices, selectedInputDeviceId);
            PreferredOutputDeviceId = selectedOutputDeviceId;
            PreferredInputDeviceId = selectedInputDeviceId;
        }

        private List<AudioDevice> CreatePreferredDeviceOptions(IEnumerable<AudioDevice> source, string selectedDeviceId)
        {
            var devices = (source ?? Enumerable.Empty<AudioDevice>())
                .Where(device => device.IsAvailable || string.Equals(device.Id, selectedDeviceId, System.StringComparison.OrdinalIgnoreCase))
                .Select(CloneDevice)
                .OrderBy(device => device.EffectiveName)
                .ToList();

            if (!string.IsNullOrWhiteSpace(selectedDeviceId) &&
                devices.All(device => !string.Equals(device.Id, selectedDeviceId, System.StringComparison.OrdinalIgnoreCase)))
            {
                devices.Add(new AudioDevice
                {
                    Id = selectedDeviceId,
                    Name = plugin?.Loc("LOCAS_UnknownDevice") ?? "Unknown device",
                    State = AudioEndpointState.Unknown,
                    StatusDisplayName = plugin?.Loc("LOCAS_DeviceStatusUnavailable") ?? "Unavailable"
                });
            }

            devices.Insert(0, new AudioDevice
            {
                Id = string.Empty,
                Name = plugin?.Loc("LOCAS_KeepWindowsDefault") ?? "Keep the Windows default",
                State = AudioEndpointState.Active
            });
            return devices;
        }

        public void RefreshGameProfiles()
        {
            AvailableGameProfiles = plugin?.GetGameProfileEntries() ?? new List<GameAudioProfileEntry>();
        }

        public List<AudioDevice> GetProfileDeviceOptions(bool input, string selectedDeviceId)
        {
            var devices = (input ? AvailableRecordingDevices : AvailablePlaybackDevices)
                .Select(CloneDevice)
                .ToList();

            if (!string.IsNullOrWhiteSpace(selectedDeviceId) &&
                devices.All(device => !string.Equals(device.Id, selectedDeviceId, System.StringComparison.OrdinalIgnoreCase)))
            {
                var customName = input ? GetInputCustomName(selectedDeviceId) : GetCustomName(selectedDeviceId);
                devices.Add(new AudioDevice
                {
                    Id = selectedDeviceId,
                    Name = string.IsNullOrWhiteSpace(customName) ? plugin?.Loc("LOCAS_UnknownDevice") ?? "Unknown device" : customName,
                    CustomName = customName,
                    State = AudioEndpointState.Unknown,
                    StatusDisplayName = plugin?.Loc("LOCAS_DeviceStatusUnavailable") ?? "Unavailable"
                });
            }

            devices = devices.OrderBy(device => device.EffectiveName).ToList();
            devices.Insert(0, new AudioDevice
            {
                Id = string.Empty,
                Name = plugin?.Loc("LOCAS_ProfileDoNotChange") ?? "Do not change",
                State = AudioEndpointState.Active
            });
            return devices;
        }

        public bool ShowDesktopBatteryIndicator
        {
            get => showDesktopBatteryIndicator;
            set => SetValue(ref showDesktopBatteryIndicator, value);
        }

        public bool ColorDesktopIndicatorByBattery
        {
            get => colorDesktopIndicatorByBattery;
            set => SetValue(ref colorDesktopIndicatorByBattery, value);
        }

        public string DesktopTopPanelIcon
        {
            get => desktopTopPanelIcon;
            set => SetValue(ref desktopTopPanelIcon, value ?? string.Empty);
        }

        public string DesktopBatteryDisplayMode
        {
            get => desktopBatteryDisplayMode;
            set
            {
                var normalized = NormalizeDesktopBatteryDisplayMode(value);
                if (!string.Equals(desktopBatteryDisplayMode, normalized, System.StringComparison.OrdinalIgnoreCase))
                {
                    SetValue(ref desktopBatteryDisplayMode, normalized);
                    OnPropertyChanged(nameof(DesktopBatteryIconOnly));
                    OnPropertyChanged(nameof(DesktopBatteryIconAndPercentage));
                    OnPropertyChanged(nameof(DesktopBatteryPercentageWithIconFallback));
                }
            }
        }

        [DontSerialize]
        public bool DesktopBatteryIconOnly
        {
            get => string.Equals(DesktopBatteryDisplayMode, "IconOnly", System.StringComparison.OrdinalIgnoreCase);
            set { if (value) DesktopBatteryDisplayMode = "IconOnly"; }
        }

        [DontSerialize]
        public bool DesktopBatteryIconAndPercentage
        {
            get => string.Equals(DesktopBatteryDisplayMode, "IconAndPercentage", System.StringComparison.OrdinalIgnoreCase);
            set { if (value) DesktopBatteryDisplayMode = "IconAndPercentage"; }
        }

        [DontSerialize]
        public bool DesktopBatteryPercentageWithIconFallback
        {
            get => string.Equals(DesktopBatteryDisplayMode, "PercentageWithIconFallback", System.StringComparison.OrdinalIgnoreCase);
            set { if (value) DesktopBatteryDisplayMode = "PercentageWithIconFallback"; }
        }

        public bool HideBatteryIndicatorWhenUnavailable
        {
            get => hideBatteryIndicatorWhenUnavailable;
            set => SetValue(ref hideBatteryIndicatorWhenUnavailable, value);
        }

        public bool ShowDisabledOutputDevices
        {
            get => showDisabledOutputDevices;
            set => SetValue(ref showDisabledOutputDevices, value);
        }

        public bool ShowDisabledInputDevices
        {
            get => showDisabledInputDevices;
            set => SetValue(ref showDisabledInputDevices, value);
        }

        public string BatteryIndicatorDisplayMode
        {
            get => batteryIndicatorDisplayMode;
            set
            {
                var normalized = NormalizeFullscreenBatteryDisplayMode(value);
                if (!string.Equals(batteryIndicatorDisplayMode, normalized, System.StringComparison.OrdinalIgnoreCase))
                {
                    SetValue(ref batteryIndicatorDisplayMode, normalized);
                    OnPropertyChanged(nameof(FullscreenBatteryIconAndPercentage));
                    OnPropertyChanged(nameof(FullscreenBatteryIconOnly));
                    OnPropertyChanged(nameof(FullscreenBatteryPercentageOnly));
                }
            }
        }

        [DontSerialize]
        public bool FullscreenBatteryIconAndPercentage
        {
            get => string.Equals(BatteryIndicatorDisplayMode, "IconAndPercentage", System.StringComparison.OrdinalIgnoreCase);
            set { if (value) BatteryIndicatorDisplayMode = "IconAndPercentage"; }
        }

        [DontSerialize]
        public bool FullscreenBatteryIconOnly
        {
            get => string.Equals(BatteryIndicatorDisplayMode, "IconOnly", System.StringComparison.OrdinalIgnoreCase);
            set { if (value) BatteryIndicatorDisplayMode = "IconOnly"; }
        }

        [DontSerialize]
        public bool FullscreenBatteryPercentageOnly
        {
            get => string.Equals(BatteryIndicatorDisplayMode, "PercentageOnly", System.StringComparison.OrdinalIgnoreCase);
            set { if (value) BatteryIndicatorDisplayMode = "PercentageOnly"; }
        }

        public string BatteryIndicatorIcon
        {
            get => batteryIndicatorIcon;
            set => SetValue(ref batteryIndicatorIcon, value);
        }

        public string AppearancePreset
        {
            get => appearancePreset;
            set
            {
                var normalized = SettingsAppearance.Normalize(value);
                if (string.Equals(appearancePreset, normalized, System.StringComparison.Ordinal))
                {
                    return;
                }

                SetValue(ref appearancePreset, normalized);
            }
        }

        public bool SetupWizardCompleted
        {
            get => setupWizardCompleted;
            set => SetValue(ref setupWizardCompleted, value);
        }

        public int SettingsSchemaVersion
        {
            get => settingsSchemaVersion;
            set => SetValue(ref settingsSchemaVersion, value);
        }

        [DontSerialize]
        public List<AppearancePresetOption> AppearancePresetOptions => new List<AppearancePresetOption>
        {
            new AppearancePresetOption { Value = SettingsAppearance.Midnight, DisplayName = plugin?.Loc("LOCAS_PresetMidnight") ?? "Midnight" },
            new AppearancePresetOption { Value = SettingsAppearance.Paper, DisplayName = plugin?.Loc("LOCAS_PresetPaper") ?? "Paper" },
            new AppearancePresetOption { Value = SettingsAppearance.Oled, DisplayName = plugin?.Loc("LOCAS_PresetOled") ?? "OLED" },
            new AppearancePresetOption { Value = SettingsAppearance.Ocean, DisplayName = plugin?.Loc("LOCAS_PresetOcean") ?? "Ocean" },
            new AppearancePresetOption { Value = SettingsAppearance.Ember, DisplayName = plugin?.Loc("LOCAS_PresetEmber") ?? "Ember" }
        };

        private void MigrateSettings(bool hadSavedSettings)
        {
            var originalSchema = SettingsSchemaVersion;
            if (hadSavedSettings && originalSchema < CurrentSettingsSchemaVersion)
            {
                // Existing installs already configured; don't force the first-run wizard.
                SetupWizardCompleted = true;
            }

            if (SettingsSchemaVersion < CurrentSettingsSchemaVersion)
            {
                SettingsSchemaVersion = CurrentSettingsSchemaVersion;
            }
        }

        private static string MigrateDesktopBatteryDisplayMode(string value)
        {
            return string.Equals(value, "PercentageOnly", System.StringComparison.OrdinalIgnoreCase)
                ? "PercentageWithIconFallback"
                : NormalizeDesktopBatteryDisplayMode(value);
        }

        private static string NormalizeDesktopBatteryDisplayMode(string value)
        {
            if (string.Equals(value, "IconOnly", System.StringComparison.OrdinalIgnoreCase))
            {
                return "IconOnly";
            }

            if (string.Equals(value, "PercentageWithIconFallback", System.StringComparison.OrdinalIgnoreCase))
            {
                return "PercentageWithIconFallback";
            }

            return "IconAndPercentage";
        }

        private static string NormalizeFullscreenBatteryDisplayMode(string value)
        {
            if (string.Equals(value, "IconOnly", System.StringComparison.OrdinalIgnoreCase))
            {
                return "IconOnly";
            }

            if (string.Equals(value, "PercentageOnly", System.StringComparison.OrdinalIgnoreCase))
            {
                return "PercentageOnly";
            }

            return "IconAndPercentage";
        }

        public List<AudioProcessOption> GetAudioProcessOptions(string selectedProcessName)
        {
            var options = new List<AudioProcessOption>
            {
                new AudioProcessOption
                {
                    ProcessName = string.Empty,
                    DisplayName = plugin?.Loc("LOCAS_AudioProcessAutomatic") ?? "Automatic detection"
                }
            };

            if (plugin == null)
            {
                return options;
            }

            try
            {
                var currentProcessId = (uint)System.Diagnostics.Process.GetCurrentProcess().Id;
                var sessions = plugin.AudioDevices.GetPlaybackAudioSessions()
                    .Where(session => session.IsActive &&
                        session.ProcessId > 0 &&
                        session.ProcessId != currentProcessId &&
                        !string.IsNullOrWhiteSpace(session.ProcessName))
                    .GroupBy(session => session.ProcessName, System.StringComparer.OrdinalIgnoreCase)
                    .Select(group => group.OrderByDescending(session => session.IsActive).First())
                    .OrderBy(session => session.FriendlyName)
                    .ThenBy(session => session.ProcessName);

                foreach (var session in sessions)
                {
                    options.Add(new AudioProcessOption
                    {
                        ProcessName = session.ProcessName,
                        DisplayName = $"{session.FriendlyName} — {session.ProcessName}.exe (PID {session.ProcessId})"
                    });
                }
            }
            catch
            {
            }

            if (!string.IsNullOrWhiteSpace(selectedProcessName) &&
                options.All(option => !string.Equals(option.ProcessName, selectedProcessName, System.StringComparison.OrdinalIgnoreCase)))
            {
                options.Insert(1, new AudioProcessOption
                {
                    ProcessName = selectedProcessName,
                    DisplayName = $"{selectedProcessName}.exe"
                });
            }

            return options;
        }

        public string GetCustomName(string deviceId)
        {
            return GetCustomName(DeviceAliases, deviceId);
        }

        public string GetInputCustomName(string deviceId)
        {
            return GetCustomName(InputDeviceAliases, deviceId);
        }

        public string GetIcon(string deviceId)
        {
            return ResolveIconId(GetIcon(DeviceAliases, deviceId));
        }

        public string GetInputIcon(string deviceId)
        {
            return ResolveIconId(GetIcon(InputDeviceAliases, deviceId));
        }

        public bool IsDeviceVisible(string deviceId)
        {
            return IsDeviceVisible(DeviceAliases, deviceId);
        }

        public bool IsInputDeviceVisible(string deviceId)
        {
            return IsDeviceVisible(InputDeviceAliases, deviceId);
        }

        public int? GetDefaultVolumePercent(string deviceId)
        {
            return GetDefaultVolumePercent(DeviceAliases, deviceId);
        }

        public int? GetDefaultInputVolumePercent(string deviceId)
        {
            return GetDefaultVolumePercent(InputDeviceAliases, deviceId);
        }

        public bool HasCustomName(string deviceId)
        {
            return !string.IsNullOrWhiteSpace(GetCustomName(deviceId));
        }

        public string SuggestIconForDevice(string deviceName, bool isInput)
        {
            var text = (deviceName ?? string.Empty).ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(text))
            {
                return isInput ? "mic" : "volume-2";
            }

            if (text.Contains("webcam") || text.Contains("camera") || text.Contains("camara") || text.Contains("cámara"))
            {
                return "webcam";
            }

            if (text.Contains("airpod") || text.Contains("earbud") || text.Contains("ear bud") ||
                text.Contains("galaxy bud") || text.Contains("pixel bud"))
            {
                return "device-airpods";
            }

            if (text.Contains("earphone") || text.Contains("in-ear") || text.Contains("in ear"))
            {
                return "earphone-bluetooth";
            }

            if (text.Contains("headset") || text.Contains("auricular") || text.Contains("headphone") || text.Contains("headphones"))
            {
                return isInput ? "headset" : "headphones";
            }

            if (text.Contains("microphone") || text.Contains("microfono") || text.Contains("micrófono") || text.Contains("mic "))
            {
                return "mic";
            }

            if (text.Contains("bluetooth") || text.Contains("wireless"))
            {
                return "bluetooth";
            }

            if (text.Contains("hdmi"))
            {
                return "hdmi-port";
            }

            if (text.Contains("chromecast") || text.Contains("airplay") || text.Contains("cast device") ||
                text.Contains("wireless display") || text.Contains("projector") || text.Contains("proyector"))
            {
                return "cast";
            }

            if (text.Contains("usb"))
            {
                return "usb";
            }

            if (text.Contains("monitor") || text.Contains("display") || text.Contains("laptop") ||
                text.Contains("pc") || text.Contains("realtek"))
            {
                return isInput ? "mic" : "monitor";
            }

            if (text.Contains("tv") || text.Contains("television") || text.Contains("televisión"))
            {
                return "tv";
            }

            if (text.Contains("speaker") || text.Contains("altavoz") || text.Contains("speakers") ||
                text.Contains("car audio") || text.Contains("automotive") || text.Contains("coche"))
            {
                return "speaker";
            }

            if (text.Contains("capture") || text.Contains("captura"))
            {
                return "capture";
            }

            if (text.Contains("virtual") || text.Contains("voicemeeter") || text.Contains("vb-audio") ||
                text.Contains("virtual cable"))
            {
                return "audio-waveform";
            }

            if (text.Contains("phone") || text.Contains("smartphone") || text.Contains("tablet"))
            {
                return "bluetooth";
            }

            return isInput ? "mic" : DefaultVolumeIconId;
        }

        public string ResolveIconId(string icon)
        {
            if (string.IsNullOrWhiteSpace(icon))
            {
                return string.Empty;
            }

            foreach (var option in IconOptions)
            {
                if (!string.Equals(option.Id, icon, System.StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (string.IsNullOrEmpty(option.Id) || !string.IsNullOrWhiteSpace(option.GeometryData))
                {
                    return option.Id;
                }

                break;
            }

            return DefaultVolumeIconId;
        }

        public void BeginEdit()
        {
            RefreshGameProfiles();
            RefreshDevices();
            editingClone = Clone();
        }

        public void CancelEdit()
        {
            if (editingClone == null)
            {
                return;
            }

            DeviceAliases = editingClone.DeviceAliases;
            InputDeviceAliases = editingClone.InputDeviceAliases;
            PreferredOutputDeviceId = editingClone.PreferredOutputDeviceId;
            PreferredInputDeviceId = editingClone.PreferredInputDeviceId;
            DeviceDisplayMode = editingClone.DeviceDisplayMode;
            ShowNotifications = editingClone.ShowNotifications;
            ShowOutputDeviceNotifications = editingClone.ShowOutputDeviceNotifications;
            ShowInputDeviceNotifications = editingClone.ShowInputDeviceNotifications;
            ShowVolumeNotifications = editingClone.ShowVolumeNotifications;
            ShowMuteNotifications = editingClone.ShowMuteNotifications;
            ShowGameProfileNotifications = editingClone.ShowGameProfileNotifications;
            ShowSpatialSoundNotifications = editingClone.ShowSpatialSoundNotifications;
            ShowDiagnosticNotifications = editingClone.ShowDiagnosticNotifications;
            QuickSwitchEnabled = editingClone.QuickSwitchEnabled;
            QuickSwitchAllDevices = editingClone.QuickSwitchAllDevices;
            ShowMediaSessionIcons = editingClone.ShowMediaSessionIcons;
            GameProfilesEnabled = editingClone.GameProfilesEnabled;
            RestoreDeviceAfterGameProfile = editingClone.RestoreDeviceAfterGameProfile;
            SpatialSoundIntegrationEnabled = editingClone.SpatialSoundIntegrationEnabled;
            SpatialSoundToolPath = editingClone.SpatialSoundToolPath;
            VolumeStepPercent = editingClone.VolumeStepPercent;
            ShowDesktopBatteryIndicator = editingClone.ShowDesktopBatteryIndicator;
            ColorDesktopIndicatorByBattery = editingClone.ColorDesktopIndicatorByBattery;
            DesktopTopPanelIcon = editingClone.DesktopTopPanelIcon;
            DesktopBatteryDisplayMode = editingClone.DesktopBatteryDisplayMode;
            HideBatteryIndicatorWhenUnavailable = editingClone.HideBatteryIndicatorWhenUnavailable;
            ShowDisabledOutputDevices = editingClone.ShowDisabledOutputDevices;
            ShowDisabledInputDevices = editingClone.ShowDisabledInputDevices;
            BatteryIndicatorDisplayMode = editingClone.BatteryIndicatorDisplayMode;
            BatteryIndicatorIcon = editingClone.BatteryIndicatorIcon;
            AppearancePreset = editingClone.AppearancePreset;
            SetupWizardCompleted = editingClone.SetupWizardCompleted;
            SettingsSchemaVersion = editingClone.SettingsSchemaVersion;
            AvailableGameProfiles = editingClone.AvailableGameProfiles.Select(profile => profile.Clone()).ToList();
            editingClone = null;
            RefreshDevices();
        }

        public void EndEdit()
        {
            DeviceAliases = PersistAliases(AvailablePlaybackDevices, DeviceAliases);
            InputDeviceAliases = PersistAliases(AvailableRecordingDevices, InputDeviceAliases);

            plugin.ReplaceGameProfiles(AvailableGameProfiles);

            plugin.SavePluginSettings(this);
            plugin.ReloadSettings();
            plugin.ApplyPreferredDevices();
            plugin.ApplyDefaultVolumeForCurrentDevice();
            plugin.ApplyDefaultInputVolumeForCurrentDevice();
            editingClone = null;
        }

        public bool VerifySettings(out List<string> errors)
        {
            errors = new List<string>();
            return true;
        }

        public AudioSwitcherSettings GetSerializableClone()
        {
            return Clone();
        }

        public void ExportAudioSessionDiagnostics()
        {
            plugin?.ExportAudioSessionDiagnostics();
        }

        public void ExportSettingsBackup()
        {
            plugin?.ExportSettingsBackup();
        }

        public void ImportSettingsBackup()
        {
            plugin?.ImportSettingsBackup();
        }

        private void MigrateFavoritesToAliases()
        {
            AddAliasIfMissing(FavoriteDeviceAId, FavoriteDeviceAName);
            AddAliasIfMissing(FavoriteDeviceBId, FavoriteDeviceBName);
        }

        private void ClearLegacyFavoriteSettings()
        {
            favoriteDeviceAId = null;
            favoriteDeviceAName = null;
            favoriteDeviceBId = null;
            favoriteDeviceBName = null;
        }

        private void AddAliasIfMissing(string deviceId, string customName)
        {
            if (string.IsNullOrWhiteSpace(deviceId) ||
                string.IsNullOrWhiteSpace(customName) ||
                DeviceAliases.Any(a => a.DeviceId == deviceId))
            {
                return;
            }

            DeviceAliases.Add(new AudioDeviceAlias
            {
                DeviceId = deviceId,
                CustomName = customName,
                IsVisible = null
            });
        }

        private AudioSwitcherSettings Clone()
        {
            var clone = new AudioSwitcherSettings
            {
                DeviceAliases = DeviceAliases.Select(a => new AudioDeviceAlias
                {
                    DeviceId = a.DeviceId,
                    CustomName = a.CustomName,
                    Icon = a.Icon,
                    IsVisible = a.IsVisible,
                    DefaultVolumePercent = a.DefaultVolumePercent
                }).ToList(),
                InputDeviceAliases = InputDeviceAliases.Select(a => new AudioDeviceAlias
                {
                    DeviceId = a.DeviceId,
                    CustomName = a.CustomName,
                    Icon = a.Icon,
                    IsVisible = a.IsVisible,
                    DefaultVolumePercent = a.DefaultVolumePercent
                }).ToList(),
                PreferredOutputDeviceId = PreferredOutputDeviceId,
                PreferredInputDeviceId = PreferredInputDeviceId,
                DeviceDisplayMode = DeviceDisplayMode,
                ShowNotifications = ShowNotifications,
                ShowOutputDeviceNotifications = ShowOutputDeviceNotifications,
                ShowInputDeviceNotifications = ShowInputDeviceNotifications,
                ShowVolumeNotifications = ShowVolumeNotifications,
                ShowMuteNotifications = ShowMuteNotifications,
                ShowGameProfileNotifications = ShowGameProfileNotifications,
                ShowSpatialSoundNotifications = ShowSpatialSoundNotifications,
                ShowDiagnosticNotifications = ShowDiagnosticNotifications,
                QuickSwitchEnabled = QuickSwitchEnabled,
                QuickSwitchAllDevices = QuickSwitchAllDevices,
                ShowMediaSessionIcons = ShowMediaSessionIcons,
                GameProfilesEnabled = GameProfilesEnabled,
                RestoreDeviceAfterGameProfile = RestoreDeviceAfterGameProfile,
                SpatialSoundIntegrationEnabled = SpatialSoundIntegrationEnabled,
                SpatialSoundToolPath = SpatialSoundToolPath,
                VolumeStepPercent = VolumeStepPercent,
                ShowDesktopBatteryIndicator = ShowDesktopBatteryIndicator,
                ColorDesktopIndicatorByBattery = ColorDesktopIndicatorByBattery,
                DesktopTopPanelIcon = DesktopTopPanelIcon,
                DesktopBatteryDisplayMode = DesktopBatteryDisplayMode,
                HideBatteryIndicatorWhenUnavailable = HideBatteryIndicatorWhenUnavailable,
                ShowDisabledOutputDevices = ShowDisabledOutputDevices,
                ShowDisabledInputDevices = ShowDisabledInputDevices,
                BatteryIndicatorDisplayMode = BatteryIndicatorDisplayMode,
                BatteryIndicatorIcon = BatteryIndicatorIcon,
                AppearancePreset = AppearancePreset,
                SetupWizardCompleted = SetupWizardCompleted,
                SettingsSchemaVersion = SettingsSchemaVersion
            };

            clone.AvailableGameProfiles = AvailableGameProfiles.Select(profile => profile.Clone()).ToList();
            return clone;
        }

        public SetupWizardDraft CreateWizardDraft()
        {
            return new SetupWizardDraft
            {
                PreferredOutputDeviceId = PreferredOutputDeviceId ?? string.Empty,
                PreferredInputDeviceId = PreferredInputDeviceId ?? string.Empty,
                ShowDesktopBatteryIndicator = ShowDesktopBatteryIndicator,
                QuickSwitchEnabled = QuickSwitchEnabled,
                SetupWizardCompleted = SetupWizardCompleted
            };
        }

        public void ApplyWizardDraft(SetupWizardDraft draft)
        {
            if (draft == null)
            {
                return;
            }

            PreferredOutputDeviceId = draft.PreferredOutputDeviceId ?? string.Empty;
            PreferredInputDeviceId = draft.PreferredInputDeviceId ?? string.Empty;
            ShowDesktopBatteryIndicator = draft.ShowDesktopBatteryIndicator;
            QuickSwitchEnabled = draft.QuickSwitchEnabled;
            SetupWizardCompleted = true;
            SettingsSchemaVersion = CurrentSettingsSchemaVersion;
        }

        internal void RefreshEditingCloneAfterExternalChange()
        {
            if (editingClone != null)
            {
                editingClone = Clone();
            }
        }

        private List<AudioDevice> RefreshDeviceList(
            List<AudioDeviceAlias> aliasesSource,
            System.Func<IReadOnlyList<AudioDevice>> getDevices,
            bool isInput)
        {
            try
            {
                var aliases = aliasesSource
                    .Where(a => !string.IsNullOrWhiteSpace(a.DeviceId))
                    .GroupBy(a => a.DeviceId)
                    .ToDictionary(a => a.Key, a => a.Last());
                var windowsDevices = (getDevices() ?? new AudioDevice[0]).ToList();
                var settingsDevices = windowsDevices
                    .Where(device => ShouldShowInSettingsDeviceList(device, isInput))
                    .OrderBy(a => a.Name)
                    .Select(device =>
                    {
                        if (aliases.TryGetValue(device.Id, out var alias))
                        {
                            device.CustomName = SanitizeCustomName(alias.CustomName);
                            device.Icon = alias.Icon;
                            device.IsVisible = alias.IsVisible != false;
                            device.DefaultVolumePercent = alias.DefaultVolumePercent;
                        }

                        if (string.IsNullOrWhiteSpace(device.Icon))
                        {
                            device.Icon = SuggestIconForDevice(device.Name, isInput);
                            device.IsIconSuggested = true;
                        }

                        device.Icon = ResolveIconId(device.Icon);
                        if (string.IsNullOrWhiteSpace(device.Icon))
                        {
                            device.Icon = isInput ? "mic" : DefaultVolumeIconId;
                            device.IsIconSuggested = true;
                        }

                        device.SettingsDisplayName = device.TechnicalDisplayName;
                        device.StatusDisplayName = GetDeviceStatusDisplayName(device.State);
                        return device;
                    })
                    .ToList();
                LogDeviceEnumeration(isInput, windowsDevices, settingsDevices);
                return settingsDevices;
            }
            catch (System.Exception ex)
            {
                try
                {
                    Playnite.SDK.LogManager.GetLogger().Error(ex, "Failed to refresh Audio Switcher device list.");
                }
                catch
                {
                }

                return new List<AudioDevice>();
            }
        }

        private bool ShouldShowInSettingsDeviceList(AudioDevice device, bool isInput)
        {
            if (device == null)
            {
                return false;
            }

            if (device.State == AudioEndpointState.Disabled)
            {
                return isInput ? ShowDisabledInputDevices : ShowDisabledOutputDevices;
            }

            return device.IsAvailable || device.State == AudioEndpointState.Unknown;
        }

        private static void LogDeviceEnumeration(bool isInput, IReadOnlyList<AudioDevice> windowsDevices, IReadOnlyList<AudioDevice> settingsDevices)
        {
            try
            {
                var kind = isInput ? "input" : "output";
                var active = CountDeviceState(windowsDevices, AudioEndpointState.Active);
                var disabled = CountDeviceState(windowsDevices, AudioEndpointState.Disabled);
                var unplugged = CountDeviceState(windowsDevices, AudioEndpointState.Unplugged);
                var notPresent = CountDeviceState(windowsDevices, AudioEndpointState.NotPresent);
                var unknown = CountDeviceState(windowsDevices, AudioEndpointState.Unknown);
                Playnite.SDK.LogManager.GetLogger().Info(
                    $"Audio Switcher {kind} enumeration: windows={windowsDevices.Count} (active={active}, disabled={disabled}, unplugged={unplugged}, notPresent={notPresent}, unknown={unknown}); settings list={settingsDevices.Count}.");
            }
            catch
            {
            }
        }

        private static int CountDeviceState(IEnumerable<AudioDevice> devices, AudioEndpointState state)
        {
            return devices.Count(device => device.State == state);
        }

        private List<AudioDeviceAlias> PersistAliases(
            IEnumerable<AudioDevice> currentDevices,
            IEnumerable<AudioDeviceAlias> existingAliases)
        {
            var devices = (currentDevices ?? Enumerable.Empty<AudioDevice>()).ToList();
            var currentIds = new HashSet<string>(devices.Select(device => device.Id));
            return devices
                .Where(HasMeaningfulDeviceCustomization)
                .Select(ToAlias)
                .Concat((existingAliases ?? Enumerable.Empty<AudioDeviceAlias>())
                    .Where(alias => !string.IsNullOrWhiteSpace(alias.DeviceId) &&
                        !currentIds.Contains(alias.DeviceId) &&
                        HasMeaningfulAlias(alias))
                    .Select(SanitizeAlias))
                .ToList();
        }

        private static bool HasMeaningfulDeviceCustomization(AudioDevice device)
        {
            return device != null &&
                (!string.IsNullOrWhiteSpace(SanitizeCustomName(device.CustomName)) ||
                    !device.IsIconSuggested && !string.IsNullOrWhiteSpace(device.Icon) ||
                    !device.IsVisible ||
                    device.DefaultVolumePercent.HasValue);
        }

        private static bool HasMeaningfulAlias(AudioDeviceAlias alias)
        {
            return alias != null &&
                (!string.IsNullOrWhiteSpace(SanitizeCustomName(alias.CustomName)) ||
                    !string.IsNullOrWhiteSpace(alias.Icon) ||
                    alias.IsVisible == false ||
                    alias.DefaultVolumePercent.HasValue);
        }

        private AudioDeviceAlias ToAlias(AudioDevice device)
        {
            return new AudioDeviceAlias
            {
                DeviceId = device.Id,
                CustomName = SanitizeCustomName(device.CustomName),
                Icon = ResolveIconId(device.IsIconSuggested ? null : device.Icon),
                IsVisible = device.IsVisible ? (bool?)null : false,
                DefaultVolumePercent = device.DefaultVolumePercent
            };
        }

        private AudioDeviceAlias SanitizeAlias(AudioDeviceAlias alias)
        {
            return new AudioDeviceAlias
            {
                DeviceId = alias.DeviceId,
                CustomName = SanitizeCustomName(alias.CustomName),
                Icon = ResolveIconId(alias.Icon),
                IsVisible = alias.IsVisible,
                DefaultVolumePercent = alias.DefaultVolumePercent
            };
        }

        internal static string SanitizeCustomName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var trimmed = value.Trim();
            if (trimmed == "-" || trimmed == "\u2014" || trimmed == "\u2013")
            {
                return null;
            }

            return trimmed;
        }

        private string GetDeviceStatusDisplayName(AudioEndpointState state)
        {
            switch (state)
            {
                case AudioEndpointState.Active:
                    return plugin?.Loc("LOCAS_DeviceStatusAvailable") ?? "Available";
                case AudioEndpointState.Disabled:
                    return plugin?.Loc("LOCAS_DeviceStatusDisabled") ?? "Disabled";
                case AudioEndpointState.Unplugged:
                    return plugin?.Loc("LOCAS_DeviceStatusDisconnected") ?? "Disconnected";
                case AudioEndpointState.NotPresent:
                    return plugin?.Loc("LOCAS_DeviceStatusNotPresent") ?? "Not present";
                default:
                    return plugin?.Loc("LOCAS_DeviceStatusUnavailable") ?? "Unavailable";
            }
        }

        private static AudioDevice CloneDevice(AudioDevice device)
        {
            return new AudioDevice
            {
                Id = device.Id,
                ContainerId = device.ContainerId,
                Name = device.Name,
                IsDefault = device.IsDefault,
                State = device.State,
                StatusDisplayName = device.StatusDisplayName,
                CustomName = device.CustomName,
                Icon = device.Icon,
                IsIconSuggested = device.IsIconSuggested,
                IsVisible = device.IsVisible,
                DefaultVolumePercent = device.DefaultVolumePercent,
                BatteryPercent = device.BatteryPercent,
                IsBatteryCharging = device.IsBatteryCharging,
                SettingsDisplayName = device.SettingsDisplayName
            };
        }

        private static string GetCustomName(List<AudioDeviceAlias> aliases, string deviceId)
        {
            return SanitizeCustomName(aliases.FirstOrDefault(a => a.DeviceId == deviceId)?.CustomName);
        }

        private static string GetIcon(List<AudioDeviceAlias> aliases, string deviceId)
        {
            return aliases.FirstOrDefault(a => a.DeviceId == deviceId)?.Icon;
        }

        private static bool IsDeviceVisible(List<AudioDeviceAlias> aliases, string deviceId)
        {
            return aliases.FirstOrDefault(a => a.DeviceId == deviceId)?.IsVisible ?? true;
        }

        private static int? GetDefaultVolumePercent(List<AudioDeviceAlias> aliases, string deviceId)
        {
            return aliases.FirstOrDefault(a => a.DeviceId == deviceId)?.DefaultVolumePercent;
        }
    }
}
