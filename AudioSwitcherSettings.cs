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
        private List<AudioDeviceAlias> deviceAliases = new List<AudioDeviceAlias>();
        private List<AudioDeviceAlias> inputDeviceAliases = new List<AudioDeviceAlias>();
        private string favoriteDeviceAId;
        private string favoriteDeviceAName = "Favorito A";
        private string favoriteDeviceBId;
        private string favoriteDeviceBName = "Favorito B";
        private string fullscreenPreferredDeviceId;
        private string deviceDisplayMode = "TextAndIcon";
        private bool showNotifications = true;
        private bool fullscreenOnlyFavorites = true;
        private bool quickSwitchEnabled;
        private bool quickSwitchAllDevices = true;
        private bool applyFullscreenPreferredOnStartup = true;
        private bool gameProfilesEnabled = true;
        private bool restoreDeviceAfterGameProfile = true;
        private bool spatialSoundIntegrationEnabled;
        private string spatialSoundToolPath;
        private string currentSpatialSoundMode;
        private int volumeStepPercent = 5;

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
                DeviceDisplayMode = string.IsNullOrWhiteSpace(savedSettings.DeviceDisplayMode) ? "TextAndIcon" : savedSettings.DeviceDisplayMode;
                ShowNotifications = savedSettings.ShowNotifications;
                FullscreenOnlyFavorites = savedSettings.FullscreenOnlyFavorites;
                QuickSwitchEnabled = savedSettings.QuickSwitchEnabled;
                QuickSwitchAllDevices = savedSettings.QuickSwitchAllDevices;
                ApplyFullscreenPreferredOnStartup = savedSettings.ApplyFullscreenPreferredOnStartup;
                GameProfilesEnabled = savedSettings.GameProfilesEnabled;
                RestoreDeviceAfterGameProfile = savedSettings.RestoreDeviceAfterGameProfile;
                SpatialSoundIntegrationEnabled = savedSettings.SpatialSoundIntegrationEnabled;
                SpatialSoundToolPath = savedSettings.SpatialSoundToolPath;
                VolumeStepPercent = savedSettings.VolumeStepPercent <= 0 ? 5 : savedSettings.VolumeStepPercent;
            }

            MigrateFavoritesToAliases();
            RefreshDevices();
        }

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
            new AudioIconOption { Id = "volume-2", Name = "Volume", Glyph = "V+", GeometryData = "M11 4.702a.705.705 0 0 0-1.203-.498L6.413 7.587A1.4 1.4 0 0 1 5.416 8H3a1 1 0 0 0-1 1v6a1 1 0 0 0 1 1h2.416a1.4 1.4 0 0 1 .997.413l3.383 3.384A.705.705 0 0 0 11 19.298z M16 9a5 5 0 0 1 0 6 M19.364 18.364a9 9 0 0 0 0-12.728" },
            new AudioIconOption { Id = "volume-1", Name = "Volume low", Glyph = "V", GeometryData = "M11 4.702a.705.705 0 0 0-1.203-.498L6.413 7.587A1.4 1.4 0 0 1 5.416 8H3a1 1 0 0 0-1 1v6a1 1 0 0 0 1 1h2.416a1.4 1.4 0 0 1 .997.413l3.383 3.384A.705.705 0 0 0 11 19.298z M16 9a5 5 0 0 1 0 6" },
            new AudioIconOption { Id = "volume", Name = "Volume off", Glyph = "VOL", GeometryData = "M11 4.702a.705.705 0 0 0-1.203-.498L6.413 7.587A1.4 1.4 0 0 1 5.416 8H3a1 1 0 0 0-1 1v6a1 1 0 0 0 1 1h2.416a1.4 1.4 0 0 1 .997.413l3.383 3.384A.705.705 0 0 0 11 19.298z" },
            new AudioIconOption { Id = "volume-off", Name = "Volume muted", Glyph = "MUTE", GeometryData = "M16 9a5 5 0 0 1 .95 2.293 M19.364 5.636a9 9 0 0 1 1.889 9.96 m2 2 20 20 m7 7-.587.587A1.4 1.4 0 0 1 5.416 8H3a1 1 0 0 0-1 1v6a1 1 0 0 0 1 1h2.416a1.4 1.4 0 0 1 .997.413l3.383 3.384A.705.705 0 0 0 11 19.298V11 M9.828 4.172A.686.686 0 0 1 11 4.657v.686" },
            new AudioIconOption { Id = "volume-x", Name = "Volume x", Glyph = "VX", GeometryData = "M11 4.702a.705.705 0 0 0-1.203-.498L6.413 7.587A1.4 1.4 0 0 1 5.416 8H3a1 1 0 0 0-1 1v6a1 1 0 0 0 1 1h2.416a1.4 1.4 0 0 1 .997.413l3.383 3.384A.705.705 0 0 0 11 19.298z M22 9 L16 15 M16 9 L22 15" },
            new AudioIconOption { Id = "headphones", Name = "Headphones", Glyph = "HP", GeometryData = "M3 14h3a2 2 0 0 1 2 2v3a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-7a9 9 0 0 1 18 0v7a2 2 0 0 1-2 2h-1a2 2 0 0 1-2-2v-3a2 2 0 0 1 2-2h3" },
            new AudioIconOption { Id = "headset", Name = "Headset", Glyph = "HS", GeometryData = "M3 11h3a2 2 0 0 1 2 2v3a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-5Zm0 0a9 9 0 1 1 18 0m0 0v5a2 2 0 0 1-2 2h-1a2 2 0 0 1-2-2v-3a2 2 0 0 1 2-2h3Z M21 16v2a4 4 0 0 1-4 4h-5" },
            new AudioIconOption { Id = "mic", Name = "Microphone", Glyph = "MIC", GeometryData = "M12 2a3 3 0 0 0-3 3v7a3 3 0 0 0 6 0V5a3 3 0 0 0-3-3 M19 10v2a7 7 0 0 1-14 0v-2 M12 19v3 M8 22h8" },
            new AudioIconOption { Id = "mic-off", Name = "Microphone off", Glyph = "MIC-", GeometryData = "M12 19v3 M15 9.34V5a3 3 0 0 0-5.68-1.33 M16.95 16.95A7 7 0 0 1 5 12v-2 M18.89 13.23A7 7 0 0 0 19 12v-2 m2 2 20 20 M9 9v3a3 3 0 0 0 5.12 2.12" },
            new AudioIconOption { Id = "mic-vocal", Name = "Vocal mic", Glyph = "VOC", GeometryData = "m11 7.601-5.994 8.19a1 1 0 0 0 .1 1.298l.817.818a1 1 0 0 0 1.314.087L15.09 12 M16.5 21.174C15.5 20.5 14.372 20 13 20c-2.058 0-3.928 2.356-6 2-2.072-.356-2.775-3.369-1.5-4.5 M11 7 A5 5 0 1 0 21 7 A5 5 0 1 0 11 7" },
            new AudioIconOption { Id = "webcam", Name = "Webcam", Glyph = "CAM", GeometryData = "M4 10 A8 8 0 1 0 20 10 A8 8 0 1 0 4 10 M9 10 A3 3 0 1 0 15 10 A3 3 0 1 0 9 10 M7 22h10 M12 22v-4" },
            new AudioIconOption { Id = "audio-lines", Name = "Audio lines", Glyph = "EQ", GeometryData = "M2 10v3 M6 6v11 M10 3v18 M14 8v7 M18 5v13 M22 10v3" },
            new AudioIconOption { Id = "audio-waveform", Name = "Waveform", Glyph = "WAV", GeometryData = "M2 13a2 2 0 0 0 2-2V7a2 2 0 0 1 4 0v13a2 2 0 0 0 4 0V4a2 2 0 0 1 4 0v13a2 2 0 0 0 4 0v-4a2 2 0 0 1 2-2" },
            new AudioIconOption { Id = "podcast", Name = "Podcast", Glyph = "POD", GeometryData = "M13 17a1 1 0 1 0-2 0l.5 4.5a0.5 0.5 0 0 0 1 0z M16.85 18.58a9 9 0 1 0-9.7 0 M8 14a5 5 0 1 1 8 0 M11 11 A1 1 0 1 0 13 11 A1 1 0 1 0 11 11" },
            new AudioIconOption { Id = "radio", Name = "Radio", Glyph = "RAD", GeometryData = "M16.247 7.761a6 6 0 0 1 0 8.478 M19.075 4.933a10 10 0 0 1 0 14.134 M4.925 19.067a10 10 0 0 1 0-14.134 M7.753 16.239a6 6 0 0 1 0-8.478 M10 12 A2 2 0 1 0 14 12 A2 2 0 1 0 10 12" },
            new AudioIconOption { Id = "radio-receiver", Name = "Radio receiver", Glyph = "REC", GeometryData = "M5 16v2 M19 16v2 M2 8 H22 V16 H2 Z M18 12h.01" },
            new AudioIconOption { Id = "speaker", Name = "Speaker", Glyph = "SP", GeometryData = "M6 2h12a2 2 0 0 1 2 2v16a2 2 0 0 1-2 2H6a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2 M12 10a4 4 0 1 0 0 8a4 4 0 0 0 0-8 M12 6h.01" },
            new AudioIconOption { Id = "monitor-speaker", Name = "Monitor speaker", Glyph = "MS", GeometryData = "M5.5 20H8 M17 9h.01 M12 4 H22 V20 H12 Z M8 6H4a2 2 0 0 0-2 2v6a2 2 0 0 0 2 2h4 M16 15 A1 1 0 1 0 18 15 A1 1 0 1 0 16 15" },
            new AudioIconOption { Id = "boom-box", Name = "Boom box", Glyph = "BOX", GeometryData = "M4 9V5a2 2 0 0 1 2-2h12a2 2 0 0 1 2 2v4 M8 8v1 M12 8v1 M16 8v1 M2 9 H22 V21 H2 Z M6 15 A2 2 0 1 0 10 15 A2 2 0 1 0 6 15 M14 15 A2 2 0 1 0 18 15 A2 2 0 1 0 14 15" },
            new AudioIconOption { Id = "tv", Name = "TV", Glyph = "TV", GeometryData = "M17 2l-5 5l-5-5 M4 7h16a2 2 0 0 1 2 2v11a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2V9a2 2 0 0 1 2-2" },
            new AudioIconOption { Id = "monitor", Name = "Monitor", Glyph = "PC", GeometryData = "M4 3h16a2 2 0 0 1 2 2v10a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2 M8 21h8 M12 17v4" },
            new AudioIconOption { Id = "laptop", Name = "Laptop", Glyph = "LAP", GeometryData = "M18 5a2 2 0 0 1 2 2v8.526a2 2 0 0 0 .212.897l1.068 2.127a1 1 0 0 1-.9 1.45H3.62a1 1 0 0 1-.9-1.45l1.068-2.127A2 2 0 0 0 4 15.526V7a2 2 0 0 1 2-2z M20.054 15.987H3.946" },
            new AudioIconOption { Id = "pc-case", Name = "PC case", Glyph = "CASE", GeometryData = "M5 2 H19 V22 H5 Z M15 14h.01 M9 6h6 M9 10h6" },
            new AudioIconOption { Id = "smartphone", Name = "Smartphone", Glyph = "PH", GeometryData = "M5 2 H19 V22 H5 Z M12 18h.01" },
            new AudioIconOption { Id = "tablet", Name = "Tablet", Glyph = "TAB", GeometryData = "M4 2 H20 V22 H4 Z M12 18 L12.01 18" },
            new AudioIconOption { Id = "gamepad-2", Name = "Gamepad", Glyph = "GP", GeometryData = "M6 11h4 M8 9v4 M15 12h.01 M18 10h.01 M17.32 5H6.68a4 4 0 0 0-3.978 3.59c-.006.052-.01.101-.017.152C2.604 9.416 2 14.456 2 16a3 3 0 0 0 3 3c1 0 1.5-.5 2-1l1.414-1.414A2 2 0 0 1 9.828 16h4.344a2 2 0 0 1 1.414.586L17 18c.5.5 1 1 2 1a3 3 0 0 0 3-3c0-1.545-.604-6.584-.685-7.258-.007-.05-.011-.1-.017-.151A4 4 0 0 0 17.32 5z" },
            new AudioIconOption { Id = "bluetooth", Name = "Bluetooth", Glyph = "BT", GeometryData = "M7 7l10 10l-5 5V2l5 5L7 17" },
            new AudioIconOption { Id = "bluetooth-connected", Name = "Bluetooth connected", Glyph = "BT+", GeometryData = "m7 7 10 10-5 5V2l5 5L7 17 M18 12 L21 12 M3 12 L6 12" },
            new AudioIconOption { Id = "bluetooth-searching", Name = "Bluetooth searching", Glyph = "BT?", GeometryData = "m7 7 10 10-5 5V2l5 5L7 17 M20.83 14.83a4 4 0 0 0 0-5.66 M18 12h.01" },
            new AudioIconOption { Id = "usb", Name = "USB", Glyph = "USB", GeometryData = "M10 6a1 1 0 1 0 0 2a1 1 0 0 0 0-2 M4 19a1 1 0 1 0 0 2a1 1 0 0 0 0-2 M4.7 19.3L19 5 M21 3l-3 1l2 2z M9.26 7.68L5 12l2 5 M10 14l5 2l3.5-3.5 M18 12l1-1l1 1l-1 1z" },
            new AudioIconOption { Id = "hdmi-port", Name = "HDMI", Glyph = "HDMI", GeometryData = "M22 9a1 1 0 0 0-1-1H3a1 1 0 0 0-1 1v4a1 1 0 0 0 1 1h1l2 2h12l2-2h1a1 1 0 0 0 1-1Z M7.5 12h9" },
            new AudioIconOption { Id = "cable", Name = "Cable", Glyph = "CAB", GeometryData = "M17 19a1 1 0 0 1-1-1v-2a2 2 0 0 1 2-2h2a2 2 0 0 1 2 2v2a1 1 0 0 1-1 1z M17 21v-2 M19 14V6.5a1 1 0 0 0-7 0v11a1 1 0 0 1-7 0V10 M21 21v-2 M3 5V3 M4 10a2 2 0 0 1-2-2V6a1 1 0 0 1 1-1h4a1 1 0 0 1 1 1v2a2 2 0 0 1-2 2z M7 5V3" }
        };

        public bool ShowNotifications
        {
            get => showNotifications;
            set => SetValue(ref showNotifications, value);
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

        public void RefreshDevices()
        {
            AvailablePlaybackDevices = RefreshDeviceList(DeviceAliases, () => plugin.AudioDevices.GetPlaybackDevices(), false);
            AvailableRecordingDevices = RefreshDeviceList(InputDeviceAliases, () => plugin.AudioDevices.GetRecordingDevices(), true);
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

            if (text.Contains("usb"))
            {
                return "usb";
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
                return isInput ? "radio-receiver" : "hdmi-port";
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
            DeviceDisplayMode = editingClone.DeviceDisplayMode;
            ShowNotifications = editingClone.ShowNotifications;
            FullscreenOnlyFavorites = editingClone.FullscreenOnlyFavorites;
            QuickSwitchEnabled = editingClone.QuickSwitchEnabled;
            QuickSwitchAllDevices = editingClone.QuickSwitchAllDevices;
            ApplyFullscreenPreferredOnStartup = editingClone.ApplyFullscreenPreferredOnStartup;
            GameProfilesEnabled = editingClone.GameProfilesEnabled;
            RestoreDeviceAfterGameProfile = editingClone.RestoreDeviceAfterGameProfile;
            SpatialSoundIntegrationEnabled = editingClone.SpatialSoundIntegrationEnabled;
            SpatialSoundToolPath = editingClone.SpatialSoundToolPath;
            VolumeStepPercent = editingClone.VolumeStepPercent;
            RefreshDevices();
        }

        public void EndEdit()
        {
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
                .ToList();

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
                .ToList();

            plugin.SavePluginSettings(this);
            plugin.ReloadSettings();
            plugin.ApplyDefaultVolumeForCurrentDevice();
            plugin.ApplyDefaultInputVolumeForCurrentDevice();
        }

        public bool VerifySettings(out List<string> errors)
        {
            errors = new List<string>();
            return true;
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
            return new AudioSwitcherSettings
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
                DeviceDisplayMode = DeviceDisplayMode,
                ShowNotifications = ShowNotifications,
                FullscreenOnlyFavorites = FullscreenOnlyFavorites,
                QuickSwitchEnabled = QuickSwitchEnabled,
                QuickSwitchAllDevices = QuickSwitchAllDevices,
                ApplyFullscreenPreferredOnStartup = ApplyFullscreenPreferredOnStartup,
                GameProfilesEnabled = GameProfilesEnabled,
                RestoreDeviceAfterGameProfile = RestoreDeviceAfterGameProfile,
                SpatialSoundIntegrationEnabled = SpatialSoundIntegrationEnabled,
                SpatialSoundToolPath = SpatialSoundToolPath,
                VolumeStepPercent = VolumeStepPercent
            };
        }

        private List<AudioDevice> RefreshDeviceList(List<AudioDeviceAlias> aliasesSource, System.Func<IReadOnlyList<AudioDevice>> getDevices, bool isInput)
        {
            try
            {
                var aliases = aliasesSource
                    .Where(a => !string.IsNullOrWhiteSpace(a.DeviceId))
                    .GroupBy(a => a.DeviceId)
                    .ToDictionary(a => a.Key, a => a.Last());

                return getDevices()
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
                        return device;
                    })
                    .ToList();
            }
            catch
            {
                return new List<AudioDevice>();
            }
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
