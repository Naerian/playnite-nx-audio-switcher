using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;
using Playnite.SDK;
using Playnite.SDK.Controls;
using Playnite.SDK.Events;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;

namespace PlayniteAudioSwitcher
{
    public sealed class AudioSwitcherPlugin : GenericPlugin
    {
        private readonly ILogger logger;
        private readonly HashSet<ControllerInput> pressedInputs = new HashSet<ControllerInput>();
        private readonly Dictionary<Guid, AudioDevice> previousDevicesByGame = new Dictionary<Guid, AudioDevice>();
        private AudioSwitcherSettings settings;
        private GameAudioProfileStore gameProfiles;
        private DateTime lastQuickSwitch = DateTime.MinValue;
        private ResourceDictionary englishFallbackResources;
        private Window activeThemeSelectorWindow;
        private AudioDeviceListControl activeThemeSelectorList;

        public override Guid Id { get; } = Guid.Parse("708b6ec4-bf96-4c0d-bd9d-fe0aa04d6bf1");

        public AudioDeviceManager AudioDevices { get; } = new AudioDeviceManager();

        public AudioSwitcherPlugin(IPlayniteAPI playniteApi) : base(playniteApi)
        {
            logger = LogManager.GetLogger();
            Properties = new GenericPluginProperties
            {
                HasSettings = true
            };

            EnsureEnglishFallbackResources();

            AddCustomElementSupport(new AddCustomElementSupportArgs
            {
                SourceName = "AudioSwitcher",
                ElementList = new List<string>
                {
                    "AudioSwitcherButton",
                    "AudioDeviceSelector",
                    "CurrentDevice",
                    "OpenSelectorButton",
                    "DeviceList"
                }
            });

            AddSettingsSupport(new AddSettingsSupportArgs
            {
                SourceName = Loc("LOCAS_PluginName"),
                SettingsRoot = "Settings"
            });

            ReloadSettings();
            gameProfiles = new GameAudioProfileStore(GetPluginUserDataPath());
        }

        public AudioSwitcherSettings Settings => settings;

        private string MenuRoot => "@" + Loc("LOCAS_PluginName");

        public string Loc(string key)
        {
            var value = PlayniteApi.Resources.GetString(key);
            if (!string.IsNullOrWhiteSpace(value) && value != key)
            {
                return value;
            }

            return GetEnglishFallbackString(key) ?? key;
        }

        private void EnsureEnglishFallbackResources()
        {
            try
            {
                englishFallbackResources = LoadEnglishFallbackResources();
                if (englishFallbackResources == null || Application.Current?.Resources == null)
                {
                    return;
                }

                var alreadyLoaded = Application.Current.Resources.MergedDictionaries
                    .OfType<ResourceDictionary>()
                    .Any(a => ReferenceEquals(a, englishFallbackResources) || a.Contains("LOCAS_PluginName") && Equals(a["LOCAS_PluginName"], "Audio Switcher"));
                if (!alreadyLoaded)
                {
                    Application.Current.Resources.MergedDictionaries.Insert(0, englishFallbackResources);
                }
            }
            catch (Exception ex)
            {
                logger?.Warn(ex, "Failed to load English fallback resources.");
            }
        }

        private ResourceDictionary LoadEnglishFallbackResources()
        {
            var path = Path.Combine(Path.GetDirectoryName(GetType().Assembly.Location), "Localization", "en_US.xaml");
            if (!File.Exists(path))
            {
                return null;
            }

            using (var stream = File.OpenRead(path))
            {
                return XamlReader.Load(stream) as ResourceDictionary;
            }
        }

        private string GetEnglishFallbackString(string key)
        {
            if (englishFallbackResources == null)
            {
                englishFallbackResources = LoadEnglishFallbackResources();
            }

            return englishFallbackResources?.Contains(key) == true ? englishFallbackResources[key]?.ToString() : null;
        }

        public void ReloadSettings()
        {
            settings = new AudioSwitcherSettings(this);
        }

        public override ISettings GetSettings(bool firstRunSettings)
        {
            return settings;
        }

        public override UserControl GetSettingsView(bool firstRunSettings)
        {
            return new AudioSwitcherSettingsView();
        }

        public override IEnumerable<MainMenuItem> GetMainMenuItems(GetMainMenuItemsArgs args)
        {
            if (PlayniteApi.ApplicationInfo.Mode == ApplicationMode.Fullscreen)
            {
                return GetFullscreenMenuItems();
            }

            var items = new List<MainMenuItem>
            {
                new MainMenuItem
                {
                    MenuSection = MenuRoot,
                    Description = Loc("LOCAS_MenuSwitchCustom"),
                    Action = _ => ToggleCustomDevices()
                },
                new MainMenuItem
                {
                    MenuSection = MenuRoot,
                    Description = Loc("LOCAS_MenuRefreshDevices"),
                    Action = _ => settings.RefreshDevices()
                }
            };

            foreach (var device in SafeGetDevices())
            {
                items.Add(new MainMenuItem
                {
                    MenuSection = $"{MenuRoot}|{Loc("LOCAS_MenuChooseOutput")}",
                    Description = device.DisplayName,
                    Action = _ => SetDevice(device.Id, GetDeviceDisplayName(device))
                });
            }

            return items;
        }

        public override IEnumerable<GameMenuItem> GetGameMenuItems(GetGameMenuItemsArgs args)
        {
            var games = args.Games?.ToList();
            if (games == null || games.Count != 1)
            {
                return Enumerable.Empty<GameMenuItem>();
            }

            var game = games[0];
            var currentProfile = gameProfiles.GetDeviceId(game);
            var root = PlayniteApi.ApplicationInfo.Mode == ApplicationMode.Fullscreen ? $"@{Loc("LOCAS_GameAudioDevice")}" : "@Audio";
            var items = new List<GameMenuItem>
            {
                new GameMenuItem
                {
                    MenuSection = root,
                    Description = GetCheckedMenuText(Loc("LOCAS_DefaultProfile"), string.IsNullOrWhiteSpace(currentProfile)),
                    Action = _ =>
                    {
                        gameProfiles.SetDevice(game, null);
                        ShowMessage($"{game.Name}: {Loc("LOCAS_DefaultAudio")}");
                    }
                }
            };

            foreach (var device in SafeGetDevicesForMenus())
            {
                var deviceId = device.Id;
                var displayName = GetDeviceDisplayName(device);
                items.Add(new GameMenuItem
                {
                    MenuSection = root,
                    Description = GetCheckedMenuText(FormatDeviceVisual(device.Icon, displayName), string.Equals(currentProfile, deviceId, StringComparison.OrdinalIgnoreCase)),
                    Action = _ =>
                    {
                        gameProfiles.SetDevice(game, deviceId);
                        ShowMessage($"{game.Name}: {displayName}");
                    }
                });
            }

            return items;
        }

        public override Control GetGameViewControl(GetGameViewControlArgs args)
        {
            if (args.Name == "AudioSwitcherButton")
            {
                return new AudioSwitcherButtonControl(this);
            }

            if (args.Name == "AudioDeviceSelector")
            {
                return new AudioDeviceSelectorControl(this);
            }

            if (args.Name == "CurrentDevice")
            {
                return new AudioCurrentDeviceControl(this);
            }

            if (args.Name == "OpenSelectorButton")
            {
                return new AudioOpenSelectorButtonControl(this);
            }

            if (args.Name == "DeviceList")
            {
                return new AudioDeviceListControl(this);
            }

            return null;
        }

        public override IEnumerable<TopPanelItem> GetTopPanelItems()
        {
            yield return new TopPanelItem
            {
                Title = Loc("LOCAS_Audio"),
                Icon = new TextBlock
                {
                    Text = "\uE995",
                    FontFamily = new FontFamily("Segoe MDL2 Assets")
                },
                Visible = true,
                Activated = ToggleCustomDevices
            };
        }

        public override IEnumerable<SidebarItem> GetSidebarItems()
        {
            yield return new SidebarItem
            {
                Title = Loc("LOCAS_Audio"),
                Type = SiderbarItemType.View,
                Icon = new TextBlock
                {
                    Text = "\uE995",
                    FontFamily = new FontFamily("Segoe MDL2 Assets")
                },
                Visible = true,
                Opened = () => new AudioDeviceSelectorPanelControl(this)
            };
        }

        public override void OnApplicationStarted(OnApplicationStartedEventArgs args)
        {
            if (PlayniteApi.ApplicationInfo.Mode == ApplicationMode.Fullscreen &&
                settings.ApplyFullscreenPreferredOnStartup &&
                !string.IsNullOrWhiteSpace(settings.FullscreenPreferredDeviceId))
            {
                SetConfiguredDevice(settings.FullscreenPreferredDeviceId, false);
            }
        }

        public override void OnGameStarting(OnGameStartingEventArgs args)
        {
            var deviceId = gameProfiles.GetDeviceId(args.Game);
            if (string.IsNullOrWhiteSpace(deviceId))
            {
                return;
            }

            try
            {
                previousDevicesByGame[args.Game.Id] = AudioDevices.GetDefaultPlaybackDevice();
                SetConfiguredDevice(deviceId, true);
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Failed to apply audio profile for {args.Game?.Name}.");
                ShowMessage($"{Loc("LOCAS_AudioProfileFailed")}: {args.Game?.Name}");
            }
        }

        public override void OnGameStopped(OnGameStoppedEventArgs args)
        {
            if (!settings.RestoreDeviceAfterGameProfile || args.Game == null)
            {
                return;
            }

            if (previousDevicesByGame.TryGetValue(args.Game.Id, out var previousDevice))
            {
                previousDevicesByGame.Remove(args.Game.Id);
                SetDevice(previousDevice.Id, GetDeviceDisplayName(previousDevice));
            }
        }

        public override void OnControllerButtonStateChanged(OnControllerButtonStateChangedArgs args)
        {
            TrackControllerInput(args);
            if (args.State == ControllerInputState.Pressed &&
                args.Button == ControllerInput.A &&
                ActivateOpenThemeSelectorDevice())
            {
                return;
            }

            if (args.State == ControllerInputState.Pressed &&
                args.Button == ControllerInput.A &&
                IsThemeSelectorFocused())
            {
                OpenThemeDeviceSelector();
                return;
            }

            if (!settings.QuickSwitchEnabled)
            {
                return;
            }

            if (args.State == ControllerInputState.Pressed &&
                pressedInputs.Contains(ControllerInput.Back) &&
                pressedInputs.Contains(ControllerInput.RightShoulder) &&
                DateTime.UtcNow - lastQuickSwitch > TimeSpan.FromMilliseconds(800))
            {
                lastQuickSwitch = DateTime.UtcNow;
                ToggleCustomDevices();
            }
        }

        private IEnumerable<MainMenuItem> GetFullscreenMenuItems()
        {
            var currentDeviceId = GetCurrentDeviceId();
            var items = new List<MainMenuItem>();

            foreach (var device in SafeGetDevices())
            {
                var deviceId = device.Id;
                var deviceName = GetDeviceDisplayName(device);
                items.Add(new MainMenuItem
                {
                    MenuSection = MenuRoot,
                    Description = GetFullscreenDeviceMenuText(device, currentDeviceId, settings.FullscreenPreferredDeviceId),
                    Action = _ => SetDevice(deviceId, deviceName)
                });
            }

            return items;
        }

        public void ToggleCustomDevices()
        {
            var switchDevices = SafeGetDevices()
                .Where(a => settings.QuickSwitchAllDevices || settings.HasCustomName(a.Id))
                .OrderBy(a => a.EffectiveName)
                .ToList();
            if (switchDevices.Count < 2)
            {
                ShowMessage(Loc("LOCAS_NeedTwoSwitchDevices"));
                OpenSettingsView();
                return;
            }

            try
            {
                var current = AudioDevices.GetDefaultPlaybackDevice();
                var currentIndex = switchDevices.FindIndex(a => string.Equals(a.Id, current?.Id, StringComparison.OrdinalIgnoreCase));
                var target = switchDevices[(currentIndex + 1 + switchDevices.Count) % switchDevices.Count];

                SetDevice(target.Id, GetDeviceDisplayName(target));
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Failed to switch between audio devices.");
                ShowMessage($"{Loc("LOCAS_AudioSwitchFailed")}: {ex.Message}");
            }
        }

        private IReadOnlyList<AudioDevice> SafeGetDevicesForMenus()
        {
            var devices = SafeGetDevices().ToList();
            if (PlayniteApi.ApplicationInfo.Mode != ApplicationMode.Fullscreen || !settings.FullscreenOnlyFavorites)
            {
                return devices;
            }

            return devices
                .Where(device => settings.HasCustomName(device.Id))
                .ToList();
        }

        public IReadOnlyList<AudioDevice> GetThemeSelectorDevices()
        {
            return SafeGetDevices()
                .Select(device =>
                {
                    device.SettingsDisplayName = GetDeviceLabel(device.Id, false, includeDefaultStar: true);
                    return device;
                })
                .ToList();
        }

        public void SetThemeSelectedDevice(string deviceId)
        {
            SetConfiguredDevice(deviceId, true);
        }

        public void OpenThemeDeviceSelector(Action onDeviceSelected = null)
        {
            try
            {
                var window = PlayniteApi.Dialogs.CreateWindow(new WindowCreationOptions
                {
                    ShowMinimizeButton = false,
                    ShowMaximizeButton = false
                });
                var parent = PlayniteApi.Dialogs.GetCurrentAppWindow();
                if (parent != null)
                {
                    window.Owner = parent;
                    window.Width = Math.Min(720, Math.Max(480, parent.Width * 0.42));
                    window.Height = Math.Min(680, Math.Max(420, parent.Height * 0.62));
                }
                else
                {
                    window.Width = 620;
                    window.Height = 560;
                }

                window.Title = Loc("LOCAS_AudioDevices");
                window.WindowStartupLocation = WindowStartupLocation.CenterOwner;

                var list = new AudioDeviceListControl(this);
                list.DeviceSelected += (_, __) =>
                {
                    onDeviceSelected?.Invoke();
                    window.Close();
                };

                var title = new TextBlock
                {
                    Text = Loc("LOCAS_AudioDevices"),
                    FontSize = 22,
                    FontWeight = FontWeights.SemiBold,
                    Margin = new Thickness(0, 0, 0, 8)
                };
                title.SetResourceReference(TextBlock.ForegroundProperty, "TextBrush");

                var help = new TextBlock
                {
                    Text = Loc("LOCAS_SelectorPanelHelp"),
                    TextWrapping = TextWrapping.Wrap,
                    Opacity = 0.75,
                    Margin = new Thickness(0, 0, 0, 16)
                };
                help.SetResourceReference(TextBlock.ForegroundProperty, "TextBrush");

                var panel = new StackPanel
                {
                    Margin = new Thickness(24)
                };
                panel.Children.Add(title);
                panel.Children.Add(help);
                panel.Children.Add(list);

                var border = new Border
                {
                    Padding = new Thickness(6),
                    Child = panel
                };
                border.SetResourceReference(Border.BackgroundProperty, "NormalBrush");
                TextElement.SetForeground(border, Application.Current?.TryFindResource("TextBrush") as Brush ?? Brushes.White);

                window.Content = border;
                activeThemeSelectorWindow = window;
                activeThemeSelectorList = list;
                window.Closed += (_, __) =>
                {
                    if (ReferenceEquals(activeThemeSelectorWindow, window))
                    {
                        activeThemeSelectorWindow = null;
                        activeThemeSelectorList = null;
                    }
                };
                window.ContentRendered += (_, __) => list.FocusFirstDevice();
                window.Dispatcher.BeginInvoke(new Action(list.FocusFirstDevice), DispatcherPriority.ApplicationIdle);
                window.ShowDialog();
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Failed to open theme audio device selector.");
                ShowMessage($"{Loc("LOCAS_AudioSwitchFailed")}: {ex.Message}");
            }
        }

        public string GetCurrentDeviceDisplayName()
        {
            try
            {
                return GetDeviceDisplayName(AudioDevices.GetDefaultPlaybackDevice());
            }
            catch
            {
                return Loc("LOCAS_Audio");
            }
        }

        public string GetCurrentDeviceDisplayLabel()
        {
            try
            {
                return GetDeviceLabel(AudioDevices.GetDefaultPlaybackDevice()?.Id, false);
            }
            catch
            {
                return Loc("LOCAS_Audio");
            }
        }

        public Geometry GetCurrentDeviceIconGeometry()
        {
            try
            {
                var current = AudioDevices.GetDefaultPlaybackDevice();
                var icon = settings.GetIcon(current?.Id);
                return string.IsNullOrWhiteSpace(icon) ? null : GetIconGeometry(icon);
            }
            catch
            {
                return null;
            }
        }

        public Geometry GetIconGeometry(string icon)
        {
            var data = settings.IconOptions.FirstOrDefault(a => string.Equals(a.Id, icon, StringComparison.OrdinalIgnoreCase))?.GeometryData;
            return string.IsNullOrWhiteSpace(data) ? null : Geometry.Parse(data);
        }

        private IEnumerable<AudioDevice> SafeGetDevices()
        {
            try
            {
                return AudioDevices.GetPlaybackDevices()
                    .Select(device =>
                    {
                        device.CustomName = settings.GetCustomName(device.Id);
                        device.Icon = settings.GetIcon(device.Id);
                        return device;
                    })
                    .ToList();
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Failed to enumerate audio devices.");
                return Enumerable.Empty<AudioDevice>();
            }
        }

        private void SetConfiguredDevice(string deviceId, bool notify)
        {
            var device = AudioDevices.GetPlaybackDevices().FirstOrDefault(a => string.Equals(a.Id, deviceId, StringComparison.OrdinalIgnoreCase));
            if (device == null)
            {
                ShowMessage(Loc("LOCAS_ConfiguredDeviceInactive"));
                return;
            }

            SetDevice(device.Id, GetDeviceDisplayName(device), notify);
        }

        private void SetDevice(string deviceId, string deviceName)
        {
            SetDevice(deviceId, deviceName, true);
        }

        private void SetDevice(string deviceId, string deviceName, bool notify)
        {
            try
            {
                AudioDevices.SetDefaultPlaybackDevice(deviceId);
                settings.RefreshDevices();
                if (notify && settings.ShowNotifications)
                {
                    ShowMessage(GetOutputNotificationText(deviceName));
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Failed to set audio device {deviceName}.");
                ShowMessage($"{Loc("LOCAS_AudioSwitchFailed")}: {ex.Message}");
            }
        }

        private void TrackControllerInput(OnControllerButtonStateChangedArgs args)
        {
            if (args.State == ControllerInputState.Pressed)
            {
                pressedInputs.Add(args.Button);
            }
            else
            {
                pressedInputs.Remove(args.Button);
            }
        }

        private bool ActivateOpenThemeSelectorDevice()
        {
            if (activeThemeSelectorWindow == null ||
                activeThemeSelectorList == null ||
                !activeThemeSelectorWindow.IsVisible)
            {
                return false;
            }

            return activeThemeSelectorList.ActivateFocusedDevice();
        }

        private string GetOutputNotificationText(string deviceName)
        {
            return PlayniteApi.ApplicationInfo.Mode == ApplicationMode.Fullscreen
                ? $"Audio: {deviceName}"
                : $"{Loc("LOCAS_AudioOutput")}: {deviceName}";
        }

        private string GetDeviceDisplayName(AudioDevice device)
        {
            return GetDeviceDisplayName(device?.Id) ?? device?.Name;
        }

        private string GetDeviceDisplayName(string deviceId)
        {
            return settings.GetCustomName(deviceId) ??
                SafeGetDevices().FirstOrDefault(a => string.Equals(a.Id, deviceId, StringComparison.OrdinalIgnoreCase))?.Name ??
                Loc("LOCAS_UnknownDevice");
        }

        private string GetCurrentDeviceId()
        {
            try
            {
                return AudioDevices.GetDefaultPlaybackDevice()?.Id;
            }
            catch
            {
                return null;
            }
        }

        private string GetDeviceLabel(string deviceId, bool includeActiveMarker, bool includeDefaultStar = false)
        {
            var device = SafeGetDevices().FirstOrDefault(a => string.Equals(a.Id, deviceId, StringComparison.OrdinalIgnoreCase));
            var name = GetDeviceDisplayName(deviceId);
            var text = FormatDeviceVisual(settings.GetIcon(deviceId), name);
            if (false && includeActiveMarker && string.Equals(deviceId, GetCurrentDeviceId(), StringComparison.OrdinalIgnoreCase))
            {
                text = "✕ " + text;
            }

            if (includeDefaultStar && device?.IsDefault == true)
            {
                text = "★ " + text;
            }

            return text;
        }

        private string FormatDeviceVisual(string icon, string name)
        {
            var iconText = GetIconText(icon);
            var hasIcon = !string.IsNullOrWhiteSpace(iconText);
            if (settings.DeviceDisplayMode == "Icon" && hasIcon)
            {
                return iconText;
            }

            if (settings.DeviceDisplayMode == "TextAndIcon" && hasIcon)
            {
                return $"{iconText} {name}";
            }

            return name;
        }

        private string GetFullscreenDeviceMenuText(AudioDevice device, string currentDeviceId, string preferredDeviceId)
        {
            var name = FormatDeviceVisual(device.Icon, GetDeviceDisplayName(device));
            if (!string.IsNullOrWhiteSpace(device.CustomName) &&
                !string.Equals(device.CustomName, device.Name, StringComparison.OrdinalIgnoreCase))
            {
                name = $"{name} - {device.Name}";
            }

            if (string.Equals(device.Id, currentDeviceId, StringComparison.OrdinalIgnoreCase))
            {
                name = "\u2713 " + name;
            }

            if (string.Equals(device.Id, preferredDeviceId, StringComparison.OrdinalIgnoreCase))
            {
                name = $"{name} \u2605";
            }

            return name;
        }

        private string GetCheckedMenuText(string text, bool isChecked)
        {
            return isChecked ? "\u2713 " + text : text;
        }

        private bool IsThemeSelectorFocused()
        {
            var focused = Keyboard.FocusedElement as DependencyObject;
            return IsElementOrParentNamed(focused, "AudioSwitcherThemeOpenSelectorButton");
        }

        private bool IsElementOrParentNamed(DependencyObject element, string name)
        {
            while (element != null)
            {
                if (element is FrameworkElement frameworkElement &&
                    string.Equals(frameworkElement.Name, name, StringComparison.Ordinal))
                {
                    return true;
                }

                element = VisualTreeHelper.GetParent(element);
            }

            return false;
        }

        private string GetLegacyFullscreenDeviceMenuText(AudioDevice device, string selectedDeviceId, bool useStarForSelected)
        {
            return GetFullscreenDeviceMenuText(device, useStarForSelected ? null : selectedDeviceId, useStarForSelected ? selectedDeviceId : null);
        }

        private string GetUnusedFullscreenDeviceMenuText(AudioDevice device, string selectedDeviceId, bool useStarForSelected)
        {
            var name = FormatDeviceVisual(device.Icon, GetDeviceDisplayName(device));
            var isSelected = string.Equals(device.Id, selectedDeviceId, StringComparison.OrdinalIgnoreCase);
            if (isSelected && useStarForSelected)
            {
                name = "â˜… " + name;
            }

            if (isSelected && !useStarForSelected)
            {
                name = $"{name} ({Loc("LOCAS_CurrentDevice")})";
            }

            if (!string.IsNullOrWhiteSpace(device.CustomName) &&
                !string.Equals(device.CustomName, device.Name, StringComparison.OrdinalIgnoreCase))
            {
                name = $"{name} - {device.Name}";
            }

            return name;
        }

        private string GetIconText(string icon)
        {
            switch (icon)
            {
                case "volume-2":
                    return "V+";
                case "volume-1":
                    return "V";
                case "headphones":
                    return "HP";
                case "speaker":
                    return "SP";
                case "tv":
                    return "TV";
                case "monitor":
                    return "PC";
                case "gamepad-2":
                    return "GP";
                case "bluetooth":
                    return "BT";
                case "usb":
                    return "USB";
                default:
                    return icon;
            }
        }

        private string GetMenuDeviceName(string deviceId, string displayName, string currentDeviceId, bool isSelectedProfile)
        {
            var prefix = string.Empty;
            if (false && string.Equals(deviceId, currentDeviceId, StringComparison.OrdinalIgnoreCase))
            {
                prefix += "✕ ";
            }

            if (isSelectedProfile)
            {
                prefix += "★ ";
            }

            return prefix + displayName;
        }

        private void ShowMessage(string message)
        {
            PlayniteApi.Notifications.Add(new NotificationMessage(
                $"AudioSwitcher-{Guid.NewGuid()}",
                message,
                NotificationType.Info));
        }
    }
}
