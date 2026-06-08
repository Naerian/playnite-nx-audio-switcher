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
        private bool quickSwitchEnabled = true;
        private bool applyFullscreenPreferredOnStartup = true;
        private bool restoreDeviceAfterGameProfile = true;

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
                ApplyFullscreenPreferredOnStartup = savedSettings.ApplyFullscreenPreferredOnStartup;
                RestoreDeviceAfterGameProfile = savedSettings.RestoreDeviceAfterGameProfile;
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
        public List<DeviceDisplayModeOption> DeviceDisplayModeOptions => new List<DeviceDisplayModeOption>
        {
            new DeviceDisplayModeOption { Id = "Text", Name = plugin?.Loc("LOCAS_DisplayModeText") ?? "Texto" },
            new DeviceDisplayModeOption { Id = "TextAndIcon", Name = plugin?.Loc("LOCAS_DisplayModeTextAndIcon") ?? "Texto e icono" },
            new DeviceDisplayModeOption { Id = "Icon", Name = plugin?.Loc("LOCAS_DisplayModeIcon") ?? "Solo icono" }
        };

        [DontSerialize]
        public List<string> IconOptions => new List<string>
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

        public bool ApplyFullscreenPreferredOnStartup
        {
            get => applyFullscreenPreferredOnStartup;
            set => SetValue(ref applyFullscreenPreferredOnStartup, value);
        }

        public bool RestoreDeviceAfterGameProfile
        {
            get => restoreDeviceAfterGameProfile;
            set => SetValue(ref restoreDeviceAfterGameProfile, value);
        }

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
            ApplyFullscreenPreferredOnStartup = editingClone.ApplyFullscreenPreferredOnStartup;
            RestoreDeviceAfterGameProfile = editingClone.RestoreDeviceAfterGameProfile;
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
                ApplyFullscreenPreferredOnStartup = ApplyFullscreenPreferredOnStartup,
                RestoreDeviceAfterGameProfile = RestoreDeviceAfterGameProfile
            };
        }
    }
}
