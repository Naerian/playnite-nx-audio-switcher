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
        private Func<bool> isThemeSelectorOpen;
        private Action closeThemeSelector;

        public override Guid Id { get; } = Guid.Parse("708b6ec4-bf96-4c0d-bd9d-fe0aa04d6bf1");

        public AudioDeviceManager AudioDevices { get; } = new AudioDeviceManager();

        public AudioSwitcherThemeApi Theme { get; }

        public AudioSwitcherPlugin(IPlayniteAPI playniteApi) : base(playniteApi)
        {
            logger = LogManager.GetLogger();
            Properties = new GenericPluginProperties
            {
                HasSettings = true
            };
            Theme = new AudioSwitcherThemeApi(this);

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
                SourceName = "AudioSwitcher",
                SettingsRoot = "Theme"
            });

            ReloadSettings();
            gameProfiles = new GameAudioProfileStore(GetPluginUserDataPath());
        }

        public AudioSwitcherSettings Settings => settings;

        private string MenuRoot => "@Audio Switcher";

        private string VisibleMenuRoot => "Audio Switcher";

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
            Theme?.Refresh();
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
            var selectedDeviceId = string.IsNullOrWhiteSpace(currentProfile)
                ? AudioDevices.GetDefaultPlaybackDevice()?.Id
                : currentProfile;
            var root = VisibleMenuRoot;
            var items = new List<GameMenuItem>();

            foreach (var device in SafeGetDevices())
            {
                var deviceId = device.Id;
                var displayName = GetDeviceDisplayName(device);
                items.Add(new GameMenuItem
                {
                    MenuSection = root,
                    Description = GetCheckedMenuText(displayName, string.Equals(selectedDeviceId, deviceId, StringComparison.OrdinalIgnoreCase)),
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
        }

        public override void OnGameStarting(OnGameStartingEventArgs args)
        {
            if (!settings.GameProfilesEnabled)
            {
                return;
            }

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
                args.Button == ControllerInput.B &&
                (CloseOpenThemeSelector() || CloseThemeApiSelector()))
            {
                return;
            }

            if (HandleThemeApiSelectorControllerInput(args))
            {
                return;
            }

            if (args.State == ControllerInputState.Pressed &&
                args.Button == ControllerInput.A &&
                TryOpenFocusedThemeSelector())
            {
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

        private bool HandleThemeApiSelectorControllerInput(OnControllerButtonStateChangedArgs args)
        {
            if (args.State != ControllerInputState.Pressed || Theme?.IsSelectorOpen != true)
            {
                return false;
            }

            if (activeThemeSelectorList != null && isThemeSelectorOpen?.Invoke() == true)
            {
                return false;
            }

            switch (args.Button)
            {
                case ControllerInput.DPadUp:
                case ControllerInput.LeftStickUp:
                    Theme.MoveHighlight(-1);
                    return true;
                case ControllerInput.DPadDown:
                case ControllerInput.LeftStickDown:
                    Theme.MoveHighlight(1);
                    return true;
                case ControllerInput.A:
                    Theme.SelectHighlightedDevice();
                    return true;
                case ControllerInput.B:
                    Theme.CloseSelector();
                    return true;
                default:
                    return true;
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
                    Description = GetFullscreenDeviceMenuText(device, currentDeviceId),
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

        public IReadOnlyList<AudioDevice> GetThemeSelectorDevices()
        {
            var currentDeviceId = GetCurrentDeviceId();

            return SafeGetDevices()
                .Select(device =>
                {
                    device.SettingsDisplayName = GetFullscreenDeviceMenuText(device, currentDeviceId);
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

                var border = new Border
                {
                    Child = list
                };
                ApplyFullscreenMenuPanelStyle(border);
                TextElement.SetForeground(border, Application.Current?.TryFindResource("TextBrush") as Brush ?? Brushes.White);

                window.Background = ResolvePanelBrush();
                window.Content = border;
                RegisterThemeSelector(list, () => window.IsVisible, window.Close);
                activeThemeSelectorWindow = window;
                window.Closed += (_, __) =>
                {
                    if (ReferenceEquals(activeThemeSelectorWindow, window))
                    {
                        activeThemeSelectorWindow = null;
                    }
                    ClearThemeSelector(list);
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

        public void RegisterThemeSelector(AudioDeviceListControl list, Func<bool> isOpen, Action close)
        {
            activeThemeSelectorList = list;
            isThemeSelectorOpen = isOpen;
            closeThemeSelector = close;
        }

        public void FocusThemeSelector()
        {
            var dispatcher = PlayniteApi?.MainView?.UIDispatcher ?? Application.Current?.Dispatcher;
            if (dispatcher == null)
            {
                return;
            }

            dispatcher.BeginInvoke(new Action(FocusThemeSelectorOnUiThread), DispatcherPriority.ApplicationIdle);
            dispatcher.BeginInvoke(new Action(FocusThemeSelectorOnUiThread), DispatcherPriority.ContextIdle);
        }

        private void FocusThemeSelectorOnUiThread()
        {
            if (activeThemeSelectorList != null && isThemeSelectorOpen?.Invoke() == true)
            {
                activeThemeSelectorList.FocusFirstDevice();
                return;
            }

            var element = FindThemeElementByName("AudioSwitcherThemeDeviceList") ??
                FindThemeElementByName("NexiumAudioSwitcherDeviceList") ??
                FindThemeElementByName("AudioSwitcher_DeviceList");
            if (element == null || !element.IsVisible)
            {
                return;
            }

            SetKeyboardFocus(element);
        }

        private FrameworkElement FindThemeElementByName(string name)
        {
            if (Application.Current?.Windows == null)
            {
                return null;
            }

            foreach (Window window in Application.Current.Windows)
            {
                var root = window.Content as DependencyObject;
                var result = FindDescendantByName<FrameworkElement>(root, name);
                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }

        private T FindDescendantByName<T>(DependencyObject root, string name) where T : FrameworkElement
        {
            if (root == null)
            {
                return null;
            }

            if (root is T element && string.Equals(element.Name, name, StringComparison.Ordinal))
            {
                return element;
            }

            var childrenCount = VisualTreeHelper.GetChildrenCount(root);
            for (var i = 0; i < childrenCount; i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                var result = FindDescendantByName<T>(child, name);
                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }

        private static void SetKeyboardFocus(FrameworkElement element)
        {
            var focusScope = FocusManager.GetFocusScope(element);
            if (focusScope != null)
            {
                FocusManager.SetFocusedElement(focusScope, element);
            }

            element.Focus();
            Keyboard.Focus(element);
        }

        public void ClearThemeSelector(AudioDeviceListControl list)
        {
            if (!ReferenceEquals(activeThemeSelectorList, list))
            {
                return;
            }

            activeThemeSelectorList = null;
            isThemeSelectorOpen = null;
            closeThemeSelector = null;
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

        public string GetDeviceDisplayNameForTheme(string deviceId)
        {
            return GetDeviceDisplayName(deviceId);
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
                Theme?.Refresh();
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
            if (activeThemeSelectorList == null ||
                isThemeSelectorOpen?.Invoke() != true)
            {
                return false;
            }

            return activeThemeSelectorList.ActivateFocusedDevice();
        }

        private bool CloseOpenThemeSelector()
        {
            if (activeThemeSelectorList == null ||
                isThemeSelectorOpen?.Invoke() != true)
            {
                return false;
            }

            closeThemeSelector?.Invoke();
            return true;
        }

        private bool CloseThemeApiSelector()
        {
            if (Theme?.IsSelectorOpen != true)
            {
                return false;
            }

            Theme.CloseSelector();
            return true;
        }

        private void ApplyFullscreenMenuPanelStyle(Border border)
        {
            var style = Application.Current?.TryFindResource("ExtensionsBorder") as Style;
            if (style != null && (style.TargetType == null || style.TargetType.IsAssignableFrom(typeof(Border))))
            {
                border.Style = style;
                return;
            }

            border.MinWidth = 430;
            border.MaxWidth = 560;
            border.MaxHeight = 650;
            border.Padding = new Thickness(22);
            border.BorderThickness = new Thickness(1);
            border.CornerRadius = new CornerRadius(12);
            border.Background = ResolvePanelBrush();
            border.BorderBrush = Application.Current?.TryFindResource("GlyphBrush") as Brush ??
                                 Application.Current?.TryFindResource("SelectionBrush") as Brush ??
                                 Brushes.White;
        }

        private Brush ResolvePanelBrush()
        {
            return Application.Current?.TryFindResource("OverlayMenuBackgroundBrush") as Brush ??
                   Application.Current?.TryFindResource("ControlBackgroundDarkBrush") as Brush ??
                   Application.Current?.TryFindResource("ControlBackgroundBrush") as Brush ??
                   new SolidColorBrush(Color.FromArgb(242, 10, 13, 20));
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

        public string GetCurrentDeviceId()
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
            return string.IsNullOrWhiteSpace(iconText) ? name : $"{iconText} {name}";
        }

        private string GetFullscreenDeviceMenuText(AudioDevice device, string currentDeviceId)
        {
            var name = GetDeviceDisplayName(device);

            if (string.Equals(device.Id, currentDeviceId, StringComparison.OrdinalIgnoreCase))
            {
                name = "\u2713 " + name;
            }

            return name;
        }

        private string GetCheckedMenuText(string text, bool isChecked)
        {
            return isChecked ? "\u2713 " + text : text;
        }

        private bool TryOpenFocusedThemeSelector()
        {
            var focused = Keyboard.FocusedElement as DependencyObject;
            var selector = GetElementOrParent<AudioOpenSelectorButtonControl>(focused);
            if (selector == null || !IsElementOrParentNamed(focused, "AudioSwitcherThemeOpenSelectorButton"))
            {
                return false;
            }

            selector.OpenSelector();
            return true;
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

        private T GetElementOrParent<T>(DependencyObject element) where T : DependencyObject
        {
            while (element != null)
            {
                if (element is T match)
                {
                    return match;
                }

                element = VisualTreeHelper.GetParent(element);
            }

            return null;
        }

        private string GetLegacyFullscreenDeviceMenuText(AudioDevice device, string selectedDeviceId, bool useStarForSelected)
        {
            return GetFullscreenDeviceMenuText(device, useStarForSelected ? null : selectedDeviceId);
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
