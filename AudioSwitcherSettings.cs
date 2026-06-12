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
        private List<AudioDeviceAlias> deviceAliases = new List<AudioDeviceAlias>();
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
            new AudioIconOption { Id = "headphones", Name = "Headphones", Glyph = "HP", GeometryData = "M3 14h3a2 2 0 0 1 2 2v3a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-7a9 9 0 0 1 18 0v7a2 2 0 0 1-2 2h-1a2 2 0 0 1-2-2v-3a2 2 0 0 1 2-2h3" },
            new AudioIconOption { Id = "speaker", Name = "Speaker", Glyph = "SP", GeometryData = "M6 2h12a2 2 0 0 1 2 2v16a2 2 0 0 1-2 2H6a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2 M12 10a4 4 0 1 0 0 8a4 4 0 0 0 0-8 M12 6h.01" },
            new AudioIconOption { Id = "tv", Name = "TV", Glyph = "TV", GeometryData = "M17 2l-5 5l-5-5 M4 7h16a2 2 0 0 1 2 2v11a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2V9a2 2 0 0 1 2-2" },
            new AudioIconOption { Id = "monitor", Name = "Monitor", Glyph = "PC", GeometryData = "M4 3h16a2 2 0 0 1 2 2v10a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2 M8 21h8 M12 17v4" },
            new AudioIconOption { Id = "gamepad-2", Name = "Gamepad", Glyph = "GP", GeometryData = "M6 11h4 M8 9v4 M15 12h.01 M18 10h.01 M17.32 5H6.68a4 4 0 0 0-3.978 3.59c-.006.052-.01.101-.017.152C2.604 9.416 2 14.456 2 16a3 3 0 0 0 3 3c1 0 1.5-.5 2-1l1.414-1.414A2 2 0 0 1 9.828 16h4.344a2 2 0 0 1 1.414.586L17 18c.5.5 1 1 2 1a3 3 0 0 0 3-3c0-1.545-.604-6.584-.685-7.258-.007-.05-.011-.1-.017-.151A4 4 0 0 0 17.32 5z" },
            new AudioIconOption { Id = "bluetooth", Name = "Bluetooth", Glyph = "BT", GeometryData = "M7 7l10 10l-5 5V2l5 5L7 17" },
            new AudioIconOption { Id = "usb", Name = "USB", Glyph = "USB", GeometryData = "M10 6a1 1 0 1 0 0 2a1 1 0 0 0 0-2 M4 19a1 1 0 1 0 0 2a1 1 0 0 0 0-2 M4.7 19.3L19 5 M21 3l-3 1l2 2z M9.26 7.68L5 12l2 5 M10 14l5 2l3.5-3.5 M18 12l1-1l1 1l-1 1z" }
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

        public string SpatialSoundToolPath
        {
            get => spatialSoundToolPath;
            set => SetValue(ref spatialSoundToolPath, value);
        }

        [DontSerialize]
        public List<SpatialSoundModeOption> SpatialSoundModeOptions => new List<SpatialSoundModeOption>
        {
            new SpatialSoundModeOption { Id = string.Empty, Name = plugin?.Loc("LOCAS_SpatialDoNotChange") ?? "Do not change" },
            new SpatialSoundModeOption { Id = "Off", Name = plugin?.Loc("LOCAS_SpatialOff") ?? "Off", ToolValue = "Off" },
            new SpatialSoundModeOption { Id = "WindowsSonicHeadphones", Name = "Windows Sonic for Headphones", ToolValue = "Windows Sonic For Headphones" },
            new SpatialSoundModeOption { Id = "DolbyAtmosHeadphones", Name = "Dolby Atmos for Headphones", ToolValue = "Dolby Atmos for Headphones" },
            new SpatialSoundModeOption { Id = "DolbyAtmosHomeTheater", Name = "Dolby Atmos for home theater", ToolValue = "Dolby Atmos for home theater" }
        };

        [DontSerialize]
        public List<AudioDevice> AvailablePlaybackDevices
        {
            get => availablePlaybackDevices;
            set => SetValue(ref availablePlaybackDevices, value);
        }

        public void RefreshDevices()
        {
            try
            {
                var aliases = DeviceAliases
                    .Where(a => !string.IsNullOrWhiteSpace(a.DeviceId))
                    .GroupBy(a => a.DeviceId)
                    .ToDictionary(a => a.Key, a => a.Last());
                AvailablePlaybackDevices = plugin.AudioDevices.GetPlaybackDevices()
                    .OrderBy(a => a.Name)
                    .Select(device =>
                    {
                        if (aliases.TryGetValue(device.Id, out var alias))
                        {
                            device.CustomName = alias.CustomName;
                            device.Icon = alias.Icon;
                        }

                        device.SettingsDisplayName = device.TechnicalDisplayName;
                        return device;
                    })
                    .ToList();
            }
            catch
            {
                AvailablePlaybackDevices = new List<AudioDevice>();
            }
        }

        public string GetCustomName(string deviceId)
        {
            return DeviceAliases.FirstOrDefault(a => a.DeviceId == deviceId)?.CustomName;
        }

        public string GetIcon(string deviceId)
        {
            return DeviceAliases.FirstOrDefault(a => a.DeviceId == deviceId)?.Icon;
        }

        public bool HasCustomName(string deviceId)
        {
            return !string.IsNullOrWhiteSpace(GetCustomName(deviceId));
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
            RefreshDevices();
        }

        public void EndEdit()
        {
            DeviceAliases = AvailablePlaybackDevices
                .Where(a => !string.IsNullOrWhiteSpace(a.CustomName))
                .Select(a => new AudioDeviceAlias
                {
                    DeviceId = a.Id,
                    CustomName = a.CustomName.Trim(),
                    Icon = a.Icon
                })
                .ToList();

            plugin.SavePluginSettings(this);
            plugin.ReloadSettings();
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
                CustomName = customName
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
                    Icon = a.Icon
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
                SpatialSoundToolPath = SpatialSoundToolPath
            };
        }
    }
}
