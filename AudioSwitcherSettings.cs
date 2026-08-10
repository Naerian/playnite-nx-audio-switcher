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
        private string favoriteDeviceAName = "Favorito A";
        private string favoriteDeviceBId;
        private string favoriteDeviceBName = "Favorito B";
        private string fullscreenPreferredDeviceId;
        private string preferredOutputDeviceId;
        private string preferredInputDeviceId;
        private string deviceDisplayMode = "TextAndIcon";
        private bool showNotifications = true;
        private bool showOutputDeviceNotifications = true;
        private bool showInputDeviceNotifications = true;
        private bool showVolumeNotifications;
        private bool showMuteNotifications;
        private bool showGameProfileNotifications = true;
        private bool showSpatialSoundNotifications = true;
        private bool showDiagnosticNotifications = true;
        private bool fullscreenOnlyFavorites = true;
        private bool quickSwitchEnabled;
        private bool quickSwitchAllDevices = true;
        private bool applyFullscreenPreferredOnStartup = true;
        private bool showMediaSessionIcons = true;
        private bool gameProfilesEnabled = true;
        private bool restoreDeviceAfterGameProfile = true;
        private bool spatialSoundIntegrationEnabled;
        private string spatialSoundToolPath;
        private string currentSpatialSoundMode;
        private int volumeStepPercent = 5;
        private bool showDesktopBatteryIndicator;
        private bool hideBatteryIndicatorWhenUnavailable = true;
        private string batteryIndicatorDisplayMode = "IconAndPercentage";
        private string batteryIndicatorIcon = string.Empty;

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
                FullscreenPreferredDeviceId = savedSettings.FullscreenPreferredDeviceId;
                PreferredOutputDeviceId = savedSettings.PreferredOutputDeviceId != null
                    ? savedSettings.PreferredOutputDeviceId
                    : savedSettings.ApplyFullscreenPreferredOnStartup ? savedSettings.FullscreenPreferredDeviceId : null;
                PreferredInputDeviceId = savedSettings.PreferredInputDeviceId;
                DeviceDisplayMode = string.IsNullOrWhiteSpace(savedSettings.DeviceDisplayMode) ? "TextAndIcon" : savedSettings.DeviceDisplayMode;
                ShowNotifications = savedSettings.ShowNotifications;
                FullscreenOnlyFavorites = savedSettings.FullscreenOnlyFavorites;
                QuickSwitchEnabled = savedSettings.QuickSwitchEnabled;
                QuickSwitchAllDevices = savedSettings.QuickSwitchAllDevices;
                ApplyFullscreenPreferredOnStartup = savedSettings.ApplyFullscreenPreferredOnStartup;
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
                VolumeStepPercent = savedSettings.VolumeStepPercent <= 0 ? 5 : savedSettings.VolumeStepPercent;
                ShowDesktopBatteryIndicator = savedSettings.ShowDesktopBatteryIndicator;
                HideBatteryIndicatorWhenUnavailable = savedSettings.HideBatteryIndicatorWhenUnavailable;
                BatteryIndicatorDisplayMode = string.IsNullOrWhiteSpace(savedSettings.BatteryIndicatorDisplayMode)
                    ? "IconAndPercentage"
                    : savedSettings.BatteryIndicatorDisplayMode;
                BatteryIndicatorIcon = savedSettings.BatteryIndicatorIcon ?? string.Empty;
            }

            MigrateFavoritesToAliases();
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

        public string FullscreenPreferredDeviceId
        {
            get => fullscreenPreferredDeviceId;
            set => SetValue(ref fullscreenPreferredDeviceId, value);
        }

        public string PreferredOutputDeviceId
        {
            get => preferredOutputDeviceId;
            set => SetValue(ref preferredOutputDeviceId, value);
        }

        public string PreferredInputDeviceId
        {
            get => preferredInputDeviceId;
            set => SetValue(ref preferredInputDeviceId, value);
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
            IconOption("radio-receiver", "Wireless receiver", "REC", "antenna.svg"),
            IconOption("boom-box", "Audio player", "BOX", "device-audio-tape.svg"),
            IconOption("vinyl", "Turntable", "VIN", "vinyl.svg"),
            IconOption("speaker", "Speaker", "SP", "device-speaker.svg"),
            IconOption("speaker-off", "Speaker unavailable", "SP-", "device-speaker-off.svg"),
            IconOption("monitor-speaker", "Monitor speakers", "MS", "device-imac.svg"),
            IconOption("speakerphone", "Speakerphone", "CALL", "speakerphone.svg"),
            IconOption("tv", "TV", "TV", "device-tv.svg"),
            IconOption("tv-old", "Classic TV", "TV2", "device-tv-old.svg"),
            IconOption("monitor", "Monitor", "PC", "device-desktop.svg"),
            IconOption("laptop", "Laptop", "LAP", "device-laptop.svg"),
            IconOption("pc-case", "PC", "CASE", "server.svg"),
            IconOption("smartphone", "Smartphone", "PH", "device-mobile.svg"),
            IconOption("tablet", "Tablet", "TAB", "device-tablet.svg"),
            IconOption("projector", "Projector", "PROJ", "device-projector.svg"),
            IconOption("car", "Car audio", "CAR", "car.svg"),
            IconOption("gamepad-2", "Gamepad", "GP", "device-gamepad.svg"),
            IconOption("bluetooth", "Bluetooth", "BT", "bluetooth.svg"),
            IconOption("bluetooth-connected", "Bluetooth connected", "BT+", "bluetooth-connected.svg"),
            IconOption("bluetooth-searching", "Bluetooth unavailable", "BT-", "bluetooth-off.svg"),
            IconOption("usb", "USB", "USB", "usb.svg"),
            IconOption("hdmi-port", "HDMI", "HDMI", "plug.svg"),
            IconOption("cable", "Cable", "CAB", "plug-connected.svg"),
            IconOption("cast", "Cast device", "CAST", "cast.svg"),
            IconOption("video", "Video device", "VID", "video.svg")
        };

        [DontSerialize]
        public List<AudioIconOption> BatteryIconOptions
        {
            get
            {
                var options = IconOptions;
                if (options.Count > 0)
                {
                    options[0].Name = plugin?.Loc("LOCAS_DeviceIcon") ?? "Device icon";
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

        public bool FullscreenOnlyFavorites
        {
            get => fullscreenOnlyFavorites;
            set => SetValue(ref fullscreenOnlyFavorites, value);
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

        public bool ApplyFullscreenPreferredOnStartup
        {
            get => applyFullscreenPreferredOnStartup;
            set => SetValue(ref applyFullscreenPreferredOnStartup, value);
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
            var profileOutputIds = new HashSet<string>(AvailableGameProfiles
                .Where(profile => !string.IsNullOrWhiteSpace(profile.DeviceId))
                .Select(profile => profile.DeviceId));
            var profileInputIds = new HashSet<string>(AvailableGameProfiles
                .Where(profile => !string.IsNullOrWhiteSpace(profile.InputDeviceId))
                .Select(profile => profile.InputDeviceId));
            AvailablePlaybackDevices = RefreshDeviceList(DeviceAliases, () => plugin.AudioDevices.GetAllPlaybackDevices(), false, profileOutputIds);
            AvailableRecordingDevices = RefreshDeviceList(InputDeviceAliases, () => plugin.AudioDevices.GetAllRecordingDevices(), true, profileInputIds);
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

        public bool HideBatteryIndicatorWhenUnavailable
        {
            get => hideBatteryIndicatorWhenUnavailable;
            set => SetValue(ref hideBatteryIndicatorWhenUnavailable, value);
        }

        public string BatteryIndicatorDisplayMode
        {
            get => batteryIndicatorDisplayMode;
            set => SetValue(ref batteryIndicatorDisplayMode, value);
        }

        public string BatteryIndicatorIcon
        {
            get => batteryIndicatorIcon;
            set => SetValue(ref batteryIndicatorIcon, value);
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
            return GetIcon(DeviceAliases, deviceId);
        }

        public string GetInputIcon(string deviceId)
        {
            return GetIcon(InputDeviceAliases, deviceId);
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
                text.Contains("wireless display"))
            {
                return "cast";
            }

            if (text.Contains("usb"))
            {
                return "usb";
            }

            if (text.Contains("projector") || text.Contains("proyector"))
            {
                return "projector";
            }

            if (text.Contains("monitor") || text.Contains("display"))
            {
                return "monitor-speaker";
            }

            if (text.Contains("tv") || text.Contains("television") || text.Contains("televisión"))
            {
                return "tv";
            }

            if (text.Contains("speaker") || text.Contains("altavoz") || text.Contains("speakers"))
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

            if (text.Contains("car audio") || text.Contains("automotive") || text.Contains("coche"))
            {
                return "car";
            }

            if (text.Contains("phone") || text.Contains("smartphone"))
            {
                return "smartphone";
            }

            if (text.Contains("tablet"))
            {
                return "tablet";
            }

            if (text.Contains("laptop"))
            {
                return "laptop";
            }

            if (text.Contains("pc") || text.Contains("realtek"))
            {
                return isInput ? "mic" : "pc-case";
            }

            return isInput ? "mic" : "volume-2";
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

            FavoriteDeviceAId = editingClone.FavoriteDeviceAId;
            FavoriteDeviceAName = editingClone.FavoriteDeviceAName;
            FavoriteDeviceBId = editingClone.FavoriteDeviceBId;
            FavoriteDeviceBName = editingClone.FavoriteDeviceBName;
            DeviceAliases = editingClone.DeviceAliases;
            InputDeviceAliases = editingClone.InputDeviceAliases;
            FullscreenPreferredDeviceId = editingClone.FullscreenPreferredDeviceId;
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
            FullscreenOnlyFavorites = editingClone.FullscreenOnlyFavorites;
            QuickSwitchEnabled = editingClone.QuickSwitchEnabled;
            QuickSwitchAllDevices = editingClone.QuickSwitchAllDevices;
            ApplyFullscreenPreferredOnStartup = editingClone.ApplyFullscreenPreferredOnStartup;
            ShowMediaSessionIcons = editingClone.ShowMediaSessionIcons;
            GameProfilesEnabled = editingClone.GameProfilesEnabled;
            RestoreDeviceAfterGameProfile = editingClone.RestoreDeviceAfterGameProfile;
            SpatialSoundIntegrationEnabled = editingClone.SpatialSoundIntegrationEnabled;
            SpatialSoundToolPath = editingClone.SpatialSoundToolPath;
            VolumeStepPercent = editingClone.VolumeStepPercent;
            ShowDesktopBatteryIndicator = editingClone.ShowDesktopBatteryIndicator;
            HideBatteryIndicatorWhenUnavailable = editingClone.HideBatteryIndicatorWhenUnavailable;
            BatteryIndicatorDisplayMode = editingClone.BatteryIndicatorDisplayMode;
            BatteryIndicatorIcon = editingClone.BatteryIndicatorIcon;
            AvailableGameProfiles = editingClone.AvailableGameProfiles.Select(profile => profile.Clone()).ToList();
            RefreshDevices();
        }

        public void EndEdit()
        {
            var playbackDeviceIds = new HashSet<string>(AvailablePlaybackDevices.Select(device => device.Id));
            DeviceAliases = AvailablePlaybackDevices
                .Where(a => !string.IsNullOrWhiteSpace(a.CustomName) ||
                    !a.IsIconSuggested && !string.IsNullOrWhiteSpace(a.Icon) ||
                    !a.IsVisible ||
                    a.DefaultVolumePercent.HasValue)
                .Select(a => new AudioDeviceAlias
                {
                    DeviceId = a.Id,
                    CustomName = a.CustomName?.Trim(),
                    Icon = a.IsIconSuggested ? null : a.Icon,
                    IsVisible = a.IsVisible ? (bool?)null : false,
                    DefaultVolumePercent = a.DefaultVolumePercent
                })
                .Concat(DeviceAliases.Where(alias => !playbackDeviceIds.Contains(alias.DeviceId)))
                .ToList();

            var recordingDeviceIds = new HashSet<string>(AvailableRecordingDevices.Select(device => device.Id));
            InputDeviceAliases = AvailableRecordingDevices
                .Where(a => !string.IsNullOrWhiteSpace(a.CustomName) ||
                    !a.IsIconSuggested && !string.IsNullOrWhiteSpace(a.Icon) ||
                    !a.IsVisible ||
                    a.DefaultVolumePercent.HasValue)
                .Select(a => new AudioDeviceAlias
                {
                    DeviceId = a.Id,
                    CustomName = a.CustomName?.Trim(),
                    Icon = a.IsIconSuggested ? null : a.Icon,
                    IsVisible = a.IsVisible ? (bool?)null : false,
                    DefaultVolumePercent = a.DefaultVolumePercent
                })
                .Concat(InputDeviceAliases.Where(alias => !recordingDeviceIds.Contains(alias.DeviceId)))
                .ToList();

            plugin.ReplaceGameProfiles(AvailableGameProfiles);

            plugin.SavePluginSettings(this);
            plugin.ReloadSettings();
            plugin.ApplyPreferredDevices();
            plugin.ApplyDefaultVolumeForCurrentDevice();
            plugin.ApplyDefaultInputVolumeForCurrentDevice();
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
                FavoriteDeviceAId = FavoriteDeviceAId,
                FavoriteDeviceAName = FavoriteDeviceAName,
                FavoriteDeviceBId = FavoriteDeviceBId,
                FavoriteDeviceBName = FavoriteDeviceBName,
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
                FullscreenPreferredDeviceId = FullscreenPreferredDeviceId,
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
                FullscreenOnlyFavorites = FullscreenOnlyFavorites,
                QuickSwitchEnabled = QuickSwitchEnabled,
                QuickSwitchAllDevices = QuickSwitchAllDevices,
                ApplyFullscreenPreferredOnStartup = ApplyFullscreenPreferredOnStartup,
                ShowMediaSessionIcons = ShowMediaSessionIcons,
                GameProfilesEnabled = GameProfilesEnabled,
                RestoreDeviceAfterGameProfile = RestoreDeviceAfterGameProfile,
                SpatialSoundIntegrationEnabled = SpatialSoundIntegrationEnabled,
                SpatialSoundToolPath = SpatialSoundToolPath,
                VolumeStepPercent = VolumeStepPercent,
                ShowDesktopBatteryIndicator = ShowDesktopBatteryIndicator,
                HideBatteryIndicatorWhenUnavailable = HideBatteryIndicatorWhenUnavailable,
                BatteryIndicatorDisplayMode = BatteryIndicatorDisplayMode,
                BatteryIndicatorIcon = BatteryIndicatorIcon
            };

            clone.AvailableGameProfiles = AvailableGameProfiles.Select(profile => profile.Clone()).ToList();
            return clone;
        }

        private List<AudioDevice> RefreshDeviceList(
            List<AudioDeviceAlias> aliasesSource,
            System.Func<IReadOnlyList<AudioDevice>> getDevices,
            bool isInput,
            ISet<string> profileDeviceIds)
        {
            try
            {
                var aliases = aliasesSource
                    .Where(a => !string.IsNullOrWhiteSpace(a.DeviceId))
                    .GroupBy(a => a.DeviceId)
                    .ToDictionary(a => a.Key, a => a.Last());
                var retainedInactiveIds = new HashSet<string>(aliases.Keys);
                retainedInactiveIds.UnionWith(profileDeviceIds ?? new HashSet<string>());

                return getDevices()
                    .Where(device => device.IsAvailable || retainedInactiveIds.Contains(device.Id))
                    .OrderBy(a => a.Name)
                    .Select(device =>
                    {
                        if (aliases.TryGetValue(device.Id, out var alias))
                        {
                            device.CustomName = alias.CustomName;
                            device.Icon = alias.Icon;
                            device.IsVisible = alias.IsVisible != false;
                            device.DefaultVolumePercent = alias.DefaultVolumePercent;
                        }

                        if (string.IsNullOrWhiteSpace(device.Icon))
                        {
                            device.Icon = SuggestIconForDevice(device.Name, isInput);
                            device.IsIconSuggested = true;
                        }

                        device.SettingsDisplayName = device.TechnicalDisplayName;
                        device.StatusDisplayName = GetDeviceStatusDisplayName(device.State);
                        return device;
                    })
                    .ToList();
            }
            catch
            {
                return new List<AudioDevice>();
            }
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
            return aliases.FirstOrDefault(a => a.DeviceId == deviceId)?.CustomName;
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
