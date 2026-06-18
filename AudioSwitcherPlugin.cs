using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        private readonly Dictionary<Guid, AudioDevice> previousInputDevicesByGame = new Dictionary<Guid, AudioDevice>();
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
                    "DeviceList",
                    "VolumeSlider",
                    "InputDeviceList",
                    "InputVolumeSlider"
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

            foreach (var device in SafeGetDevices().Where(a => a.IsVisible))
            {
                items.Add(new MainMenuItem
                {
                    MenuSection = $"{MenuRoot}|{Loc("LOCAS_MenuChooseOutput")}",
                    Description = device.DisplayName,
                    Action = _ => SetDevice(device.Id, GetDeviceDisplayName(device))
                });
            }

            foreach (var device in SafeGetInputDevices().Where(a => a.IsVisible))
            {
                items.Add(new MainMenuItem
                {
                    MenuSection = $"{MenuRoot}|{Loc("LOCAS_MenuChooseInput")}",
                    Description = device.DisplayName,
                    Action = _ => SetInputDevice(device.Id, GetInputDeviceDisplayName(device))
                });
            }

            AddVolumeMenuItems(items);
            AddSpatialSoundMenuItems(items);

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
            var currentProfile = gameProfiles.GetProfile(game);
            var selectedDeviceId = string.IsNullOrWhiteSpace(currentProfile?.DeviceId)
                ? AudioDevices.GetDefaultPlaybackDevice()?.Id
                : currentProfile.DeviceId;
            var selectedInputDeviceId = string.IsNullOrWhiteSpace(currentProfile?.InputDeviceId)
                ? AudioDevices.GetDefaultRecordingDevice()?.Id
                : currentProfile.InputDeviceId;
            var root = VisibleMenuRoot;
            var items = new List<GameMenuItem>();

            foreach (var device in SafeGetDevices().Where(a => a.IsVisible))
            {
                var deviceId = device.Id;
                var displayName = GetDeviceDisplayName(device);
                items.Add(new GameMenuItem
                {
                    MenuSection = $"{root}|{Loc("LOCAS_MenuChooseOutput")}",
                    Description = GetCheckedMenuText(displayName, string.Equals(selectedDeviceId, deviceId, StringComparison.OrdinalIgnoreCase)),
                    Action = _ =>
                    {
                        gameProfiles.SetDevice(game, deviceId);
                        ShowGameProfileInfoMessage($"{game.Name}: {displayName}");
                    }
                });
            }

            foreach (var device in SafeGetInputDevices().Where(a => a.IsVisible))
            {
                var deviceId = device.Id;
                var displayName = GetInputDeviceDisplayName(device);
                items.Add(new GameMenuItem
                {
                    MenuSection = $"{root}|{Loc("LOCAS_MenuChooseInput")}",
                    Description = GetCheckedMenuText(displayName, string.Equals(selectedInputDeviceId, deviceId, StringComparison.OrdinalIgnoreCase)),
                    Action = _ =>
                    {
                        gameProfiles.SetInputDevice(game, deviceId);
                        ShowGameProfileInfoMessage($"{game.Name}: {displayName}");
                    }
                });
            }

            foreach (var mode in settings.SpatialSoundModeOptions)
            {
                var modeId = mode.Id;
                items.Add(new GameMenuItem
                {
                    MenuSection = $"{root}|{Loc("LOCAS_SpatialSoundTitle")}",
                    Description = GetCheckedMenuText(mode.Name, string.Equals(currentProfile?.SpatialSoundMode ?? string.Empty, modeId ?? string.Empty, StringComparison.OrdinalIgnoreCase)),
                    Action = _ =>
                    {
                        gameProfiles.SetSpatialSoundMode(game, modeId);
                        ShowGameProfileInfoMessage($"{game.Name}: {mode.Name}");
                    }
                });
            }

            items.Add(new GameMenuItem
            {
                MenuSection = root,
                Description = Loc("LOCAS_ResetGameProfile"),
                Action = _ =>
                {
                    gameProfiles.ClearProfile(game);
                    ShowGameProfileInfoMessage($"{game.Name}: {Loc("LOCAS_GameProfileReset")}");
                }
            });

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

            if (args.Name == "VolumeSlider")
            {
                return new AudioVolumeSliderControl(this);
            }

            if (args.Name == "InputDeviceList")
            {
                return new AudioInputDeviceListControl(this);
            }

            if (args.Name == "InputVolumeSlider")
            {
                return new AudioInputVolumeSliderControl(this);
            }

            return null;
        }

        public override IEnumerable<TopPanelItem> GetTopPanelItems()
        {
            return Enumerable.Empty<TopPanelItem>();
        }

        public override IEnumerable<SidebarItem> GetSidebarItems()
        {
            return Enumerable.Empty<SidebarItem>();
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

            var profile = gameProfiles.GetProfile(args.Game);
            if (profile == null ||
                string.IsNullOrWhiteSpace(profile.DeviceId) &&
                string.IsNullOrWhiteSpace(profile.InputDeviceId) &&
                string.IsNullOrWhiteSpace(profile.SpatialSoundMode))
            {
                return;
            }

            try
            {
                var appliedParts = new List<string>();

                if (!string.IsNullOrWhiteSpace(profile.DeviceId))
                {
                    previousDevicesByGame[args.Game.Id] = AudioDevices.GetDefaultPlaybackDevice();
                    SetConfiguredDevice(profile.DeviceId, false);
                    appliedParts.Add(GetDeviceDisplayName(profile.DeviceId));
                }

                if (!string.IsNullOrWhiteSpace(profile.InputDeviceId))
                {
                    previousInputDevicesByGame[args.Game.Id] = AudioDevices.GetDefaultRecordingDevice();
                    SetConfiguredInputDevice(profile.InputDeviceId, false);
                    appliedParts.Add(GetInputDeviceDisplayName(profile.InputDeviceId));
                }

                if (ApplySpatialSoundMode(profile.SpatialSoundMode, false))
                {
                    var spatialName = GetSpatialSoundModeDisplayName(profile.SpatialSoundMode);
                    if (!string.IsNullOrWhiteSpace(spatialName))
                    {
                        appliedParts.Add(spatialName);
                    }
                }

                ShowGameProfileAppliedMessage(args.Game?.Name, appliedParts);
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
                SetDevice(previousDevice.Id, GetDeviceDisplayName(previousDevice), false);
            }

            if (previousInputDevicesByGame.TryGetValue(args.Game.Id, out var previousInputDevice))
            {
                previousInputDevicesByGame.Remove(args.Game.Id);
                SetInputDevice(previousInputDevice.Id, GetInputDeviceDisplayName(previousInputDevice), false);
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

            foreach (var device in SafeGetDevices().Where(a => a.IsVisible))
            {
                var deviceId = device.Id;
                var deviceName = GetDeviceDisplayName(device);
                items.Add(new MainMenuItem
                {
                    MenuSection = $"{MenuRoot}|{Loc("LOCAS_MenuChooseOutput")}",
                    Description = GetFullscreenDeviceMenuText(device, currentDeviceId),
                    Action = _ => SetDevice(deviceId, deviceName)
                });
            }

            var currentInputDeviceId = GetCurrentInputDeviceId();
            foreach (var device in SafeGetInputDevices().Where(a => a.IsVisible))
            {
                var deviceId = device.Id;
                var deviceName = GetInputDeviceDisplayName(device);
                items.Add(new MainMenuItem
                {
                    MenuSection = $"{MenuRoot}|{Loc("LOCAS_MenuChooseInput")}",
                    Description = GetFullscreenInputDeviceMenuText(device, currentInputDeviceId),
                    Action = _ => SetInputDevice(deviceId, deviceName)
                });
            }

            AddSpatialSoundMenuItems(items);

            return items;
        }

        public void ToggleCustomDevices()
        {
            var switchDevices = SafeGetDevices()
                .Where(a => a.IsVisible)
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
            return GetThemeSelectorDevices(false);
        }

        public IReadOnlyList<AudioDevice> GetThemeSelectorDevices(bool includeHidden)
        {
            var currentDeviceId = GetCurrentDeviceId();

            return SafeGetDevices()
                .Where(device => includeHidden || device.IsVisible)
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

        public IReadOnlyList<AudioDevice> GetThemeSelectorInputDevices()
        {
            return GetThemeSelectorInputDevices(false);
        }

        public IReadOnlyList<AudioDevice> GetThemeSelectorInputDevices(bool includeHidden)
        {
            var currentDeviceId = GetCurrentInputDeviceId();

            return SafeGetInputDevices()
                .Where(device => includeHidden || device.IsVisible)
                .Select(device =>
                {
                    device.SettingsDisplayName = GetFullscreenInputDeviceMenuText(device, currentDeviceId);
                    return device;
                })
                .ToList();
        }

        public void SetThemeSelectedInputDevice(string deviceId)
        {
            SetConfiguredInputDevice(deviceId, true);
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

        public string GetCurrentInputDeviceDisplayName()
        {
            try
            {
                return GetInputDeviceDisplayName(AudioDevices.GetDefaultRecordingDevice());
            }
            catch
            {
                return Loc("LOCAS_AudioInput");
            }
        }

        public string GetCurrentInputDeviceDisplayLabel()
        {
            try
            {
                return GetInputDeviceLabel(AudioDevices.GetDefaultRecordingDevice()?.Id, false);
            }
            catch
            {
                return Loc("LOCAS_AudioInput");
            }
        }

        public string GetDeviceDisplayNameForTheme(string deviceId)
        {
            return GetDeviceDisplayName(deviceId);
        }

        public string GetInputDeviceDisplayNameForTheme(string deviceId)
        {
            return GetInputDeviceDisplayName(deviceId);
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

        public Geometry GetCurrentInputDeviceIconGeometry()
        {
            try
            {
                var current = AudioDevices.GetDefaultRecordingDevice();
                var icon = settings.GetInputIcon(current?.Id);
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

        public AudioVolumeState GetCurrentVolumeState()
        {
            return AudioDevices.GetDefaultPlaybackVolume();
        }

        public AudioVolumeState GetCurrentInputVolumeState()
        {
            return AudioDevices.GetDefaultRecordingVolume();
        }

        public void SetVolume(float volume)
        {
            SetVolume(volume, true);
        }

        public void SetVolume(float volume, bool notify)
        {
            try
            {
                AudioDevices.SetDefaultPlaybackVolume(volume);
                Theme?.Refresh();
                if (notify && ShouldShowVolumeNotifications())
                {
                    ShowVolumeInfoMessage();
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Failed to set default playback volume.");
                ShowMessage($"{Loc("LOCAS_VolumeFailed")}: {ex.Message}");
            }
        }

        public void SetInputVolume(float volume)
        {
            SetInputVolume(volume, true);
        }

        public void SetInputVolume(float volume, bool notify)
        {
            try
            {
                AudioDevices.SetDefaultRecordingVolume(volume);
                Theme?.Refresh();
                if (notify && ShouldShowVolumeNotifications())
                {
                    ShowInputVolumeInfoMessage();
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Failed to set default recording volume.");
                ShowMessage($"{Loc("LOCAS_InputVolumeFailed")}: {ex.Message}");
            }
        }

        public void ChangeVolumeByStep(int direction)
        {
            try
            {
                var step = Math.Max(1, settings.VolumeStepPercent) / 100f;
                AudioDevices.ChangeDefaultPlaybackVolume(step * Math.Sign(direction));
                Theme?.Refresh();
                if (ShouldShowVolumeNotifications())
                {
                    ShowVolumeInfoMessage();
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Failed to change default playback volume.");
                ShowMessage($"{Loc("LOCAS_VolumeFailed")}: {ex.Message}");
            }
        }

        public void ChangeInputVolumeByStep(int direction)
        {
            try
            {
                var step = Math.Max(1, settings.VolumeStepPercent) / 100f;
                AudioDevices.ChangeDefaultRecordingVolume(step * Math.Sign(direction));
                Theme?.Refresh();
                if (ShouldShowVolumeNotifications())
                {
                    ShowInputVolumeInfoMessage();
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Failed to change default recording volume.");
                ShowMessage($"{Loc("LOCAS_InputVolumeFailed")}: {ex.Message}");
            }
        }

        public void ToggleMute()
        {
            try
            {
                AudioDevices.ToggleDefaultPlaybackMute();
                Theme?.Refresh();
                var state = AudioDevices.GetDefaultPlaybackVolume();
                ShowMuteInfoMessage(state.IsMuted ? Loc("LOCAS_Muted") : Loc("LOCAS_Unmuted"));
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Failed to toggle default playback mute.");
                ShowMessage($"{Loc("LOCAS_VolumeFailed")}: {ex.Message}");
            }
        }

        public void ToggleInputMute()
        {
            try
            {
                AudioDevices.ToggleDefaultRecordingMute();
                Theme?.Refresh();
                var state = AudioDevices.GetDefaultRecordingVolume();
                ShowMuteInfoMessage(state.IsMuted ? Loc("LOCAS_InputMuted") : Loc("LOCAS_InputUnmuted"));
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Failed to toggle default recording mute.");
                ShowMessage($"{Loc("LOCAS_InputVolumeFailed")}: {ex.Message}");
            }
        }

        public void ApplyDefaultVolumeForCurrentDevice()
        {
            var currentDeviceId = GetCurrentDeviceId();
            if (!string.IsNullOrWhiteSpace(currentDeviceId))
            {
                TryApplyDefaultVolume(currentDeviceId);
                Theme?.Refresh();
            }
        }

        public void ApplyDefaultInputVolumeForCurrentDevice()
        {
            var currentDeviceId = GetCurrentInputDeviceId();
            if (!string.IsNullOrWhiteSpace(currentDeviceId))
            {
                TryApplyDefaultInputVolume(currentDeviceId);
                Theme?.Refresh();
            }
        }

        public void SetVolumeStepPercent(int value)
        {
            settings.VolumeStepPercent = Math.Max(1, Math.Min(50, value));
            SavePluginSettings(settings);
            Theme?.Refresh();
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
                        if (string.IsNullOrWhiteSpace(device.Icon))
                        {
                            device.Icon = settings.SuggestIconForDevice(device.Name, false);
                        }

                        device.IsVisible = settings.IsDeviceVisible(device.Id);
                        device.DefaultVolumePercent = settings.GetDefaultVolumePercent(device.Id);
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

        private IEnumerable<AudioDevice> SafeGetInputDevices()
        {
            try
            {
                return AudioDevices.GetRecordingDevices()
                    .Select(device =>
                    {
                        device.CustomName = settings.GetInputCustomName(device.Id);
                        device.Icon = settings.GetInputIcon(device.Id);
                        if (string.IsNullOrWhiteSpace(device.Icon))
                        {
                            device.Icon = settings.SuggestIconForDevice(device.Name, true);
                        }

                        device.IsVisible = settings.IsInputDeviceVisible(device.Id);
                        device.DefaultVolumePercent = settings.GetDefaultInputVolumePercent(device.Id);
                        return device;
                    })
                    .ToList();
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Failed to enumerate audio input devices.");
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

        private void SetConfiguredInputDevice(string deviceId, bool notify)
        {
            var device = AudioDevices.GetRecordingDevices().FirstOrDefault(a => string.Equals(a.Id, deviceId, StringComparison.OrdinalIgnoreCase));
            if (device == null)
            {
                ShowMessage(Loc("LOCAS_ConfiguredInputDeviceInactive"));
                return;
            }

            SetInputDevice(device.Id, GetInputDeviceDisplayName(device), notify);
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
                TryApplyDefaultVolume(deviceId);
                settings.RefreshDevices();
                Theme?.Refresh();
                if (notify && ShouldShowOutputDeviceNotifications())
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

        private void SetInputDevice(string deviceId, string deviceName)
        {
            SetInputDevice(deviceId, deviceName, true);
        }

        private void SetInputDevice(string deviceId, string deviceName, bool notify)
        {
            try
            {
                AudioDevices.SetDefaultRecordingDevice(deviceId);
                TryApplyDefaultInputVolume(deviceId);
                settings.RefreshDevices();
                Theme?.Refresh();
                if (notify && ShouldShowInputDeviceNotifications())
                {
                    ShowMessage($"{Loc("LOCAS_AudioInput")}: {deviceName}");
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Failed to set audio input device {deviceName}.");
                ShowMessage($"{Loc("LOCAS_InputSwitchFailed")}: {ex.Message}");
            }
        }

        private void TryApplyDefaultVolume(string deviceId)
        {
            var defaultVolume = settings.GetDefaultVolumePercent(deviceId);
            if (!defaultVolume.HasValue)
            {
                return;
            }

            try
            {
                AudioDevices.SetDefaultPlaybackVolume(defaultVolume.Value / 100f);
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Failed to apply default volume for audio device {deviceId}.");
                ShowMessage($"{Loc("LOCAS_VolumeFailed")}: {ex.Message}");
            }
        }

        private void TryApplyDefaultInputVolume(string deviceId)
        {
            var defaultVolume = settings.GetDefaultInputVolumePercent(deviceId);
            if (!defaultVolume.HasValue)
            {
                return;
            }

            try
            {
                AudioDevices.SetDefaultRecordingVolume(defaultVolume.Value / 100f);
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Failed to apply default input volume for audio device {deviceId}.");
                ShowMessage($"{Loc("LOCAS_InputVolumeFailed")}: {ex.Message}");
            }
        }

        private void AddSpatialSoundMenuItems(List<MainMenuItem> items)
        {
            if (!settings.SpatialSoundIntegrationEnabled)
            {
                return;
            }

            foreach (var mode in settings.SpatialSoundModeOptions.Where(a => !string.IsNullOrWhiteSpace(a.Id)))
            {
                var modeId = mode.Id;
                items.Add(new MainMenuItem
                {
                    MenuSection = $"{MenuRoot}|{Loc("LOCAS_SpatialSoundTitle")}",
                    Description = GetCheckedMenuText(mode.Name, IsCurrentSpatialSoundMenuMode(modeId)),
                    Action = _ => ApplySpatialSoundMode(modeId, true)
                });
            }
        }

        private void AddVolumeMenuItems(List<MainMenuItem> items)
        {
            var section = $"{MenuRoot}|{Loc("LOCAS_VolumeTitle")}";
            items.Add(new MainMenuItem
            {
                MenuSection = section,
                Description = Loc("LOCAS_VolumeUp"),
                Action = _ => ChangeVolumeByStep(1)
            });
            items.Add(new MainMenuItem
            {
                MenuSection = section,
                Description = Loc("LOCAS_VolumeDown"),
                Action = _ => ChangeVolumeByStep(-1)
            });
            items.Add(new MainMenuItem
            {
                MenuSection = section,
                Description = Loc("LOCAS_ToggleMute"),
                Action = _ => ToggleMute()
            });
        }

        private bool ApplySpatialSoundMode(string modeId, bool notify)
        {
            if (string.IsNullOrWhiteSpace(modeId))
            {
                return true;
            }

            if (!settings.SpatialSoundIntegrationEnabled)
            {
                return true;
            }

            var mode = settings.SpatialSoundModeOptions.FirstOrDefault(a => string.Equals(a.Id, modeId, StringComparison.OrdinalIgnoreCase));
            if (mode == null || mode.ToolValue == null)
            {
                return true;
            }

            var toolPath = settings.SpatialSoundToolPath;
            if (string.IsNullOrWhiteSpace(toolPath) || !File.Exists(toolPath))
            {
                ShowMessage(Loc("LOCAS_SpatialToolMissing"));
                return false;
            }

            try
            {
                var deviceArgument = ResolveSpatialSoundDeviceArgument(toolPath);
                var startInfo = new ProcessStartInfo
                {
                    FileName = toolPath,
                    Arguments = $"/SetSpatial {EscapeArgument(deviceArgument)} {EscapeArgument(mode.ToolValue)}",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };

                using (var process = Process.Start(startInfo))
                {
                    process?.WaitForExit(5000);
                    if (process != null && !process.HasExited)
                    {
                        process.Kill();
                        throw new TimeoutException("Spatial sound tool timed out.");
                    }

                    if (process != null && process.ExitCode != 0)
                    {
                        throw new InvalidOperationException($"Spatial sound tool exited with code {process.ExitCode}.");
                    }
                }

                settings.CurrentSpatialSoundMode = mode.Id;
                if (notify && ShouldShowSpatialSoundNotifications())
                {
                    ShowMessage($"{Loc("LOCAS_SpatialSoundTitle")}: {mode.Name}");
                }

                return true;
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Failed to set spatial sound mode {mode.Name}.");
                ShowMessage($"{Loc("LOCAS_SpatialSwitchFailed")}: {ex.Message}");
                return false;
            }
        }

        private bool IsCurrentSpatialSoundMenuMode(string modeId)
        {
            var currentMode = string.IsNullOrWhiteSpace(settings.CurrentSpatialSoundMode)
                ? "Off"
                : settings.CurrentSpatialSoundMode;
            return string.Equals(currentMode, modeId, StringComparison.OrdinalIgnoreCase);
        }

        private string GetSpatialSoundModeDisplayName(string modeId)
        {
            if (string.IsNullOrWhiteSpace(modeId))
            {
                return null;
            }

            return settings.SpatialSoundModeOptions
                .FirstOrDefault(a => string.Equals(a.Id, modeId, StringComparison.OrdinalIgnoreCase))
                ?.Name;
        }

        private string ResolveSpatialSoundDeviceArgument(string toolPath)
        {
            var tempFile = Path.Combine(Path.GetTempPath(), $"AudioSwitcher-SoundVolumeView-{Guid.NewGuid():N}.csv");
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = toolPath,
                    Arguments = $"/scomma {EscapeArgument(tempFile)} /Columns {EscapeArgument("Name,Command-Line Friendly ID,Direction,Device State,Default")}",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };

                using (var process = Process.Start(startInfo))
                {
                    process?.WaitForExit(5000);
                }

                if (!File.Exists(tempFile))
                {
                    return "DefaultRenderDevice";
                }

                var lines = File.ReadAllLines(tempFile);
                if (lines.Length < 2)
                {
                    return "DefaultRenderDevice";
                }

                var headers = SplitCsvLine(lines[0]);
                var friendlyIdIndex = headers.FindIndex(a => string.Equals(a, "Command-Line Friendly ID", StringComparison.OrdinalIgnoreCase));
                var directionIndex = headers.FindIndex(a => string.Equals(a, "Direction", StringComparison.OrdinalIgnoreCase));
                var stateIndex = headers.FindIndex(a => string.Equals(a, "Device State", StringComparison.OrdinalIgnoreCase));
                var defaultIndex = headers.FindIndex(a => string.Equals(a, "Default", StringComparison.OrdinalIgnoreCase));

                foreach (var line in lines.Skip(1))
                {
                    var values = SplitCsvLine(line);
                    var isRender = directionIndex >= 0 && directionIndex < values.Count && string.Equals(values[directionIndex], "Render", StringComparison.OrdinalIgnoreCase);
                    var isActive = stateIndex >= 0 && stateIndex < values.Count && string.Equals(values[stateIndex], "Active", StringComparison.OrdinalIgnoreCase);
                    var isDefault = defaultIndex >= 0 && defaultIndex < values.Count && values[defaultIndex].IndexOf("Render", StringComparison.OrdinalIgnoreCase) >= 0;
                    if (isRender && isActive && isDefault && friendlyIdIndex >= 0 && friendlyIdIndex < values.Count && !string.IsNullOrWhiteSpace(values[friendlyIdIndex]))
                    {
                        return values[friendlyIdIndex];
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Warn(ex, "Failed to resolve SoundVolumeView command-line friendly device ID.");
            }
            finally
            {
                try
                {
                    if (File.Exists(tempFile))
                    {
                        File.Delete(tempFile);
                    }
                }
                catch
                {
                }
            }

            return "DefaultRenderDevice";
        }

        private static List<string> SplitCsvLine(string line)
        {
            var values = new List<string>();
            var current = new System.Text.StringBuilder();
            var inQuotes = false;

            for (var i = 0; i < (line ?? string.Empty).Length; i++)
            {
                var c = line[i];
                if (c == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }
                }
                else if (c == ',' && !inQuotes)
                {
                    values.Add(current.ToString());
                    current.Clear();
                }
                else
                {
                    current.Append(c);
                }
            }

            values.Add(current.ToString());
            return values;
        }

        private static string EscapeArgument(string value)
        {
            return "\"" + (value ?? string.Empty).Replace("\"", "\\\"") + "\"";
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

        private string GetInputDeviceDisplayName(AudioDevice device)
        {
            return GetInputDeviceDisplayName(device?.Id) ?? device?.Name;
        }

        private string GetInputDeviceDisplayName(string deviceId)
        {
            return settings.GetInputCustomName(deviceId) ??
                SafeGetInputDevices().FirstOrDefault(a => string.Equals(a.Id, deviceId, StringComparison.OrdinalIgnoreCase))?.Name ??
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

        public string GetCurrentInputDeviceId()
        {
            try
            {
                return AudioDevices.GetDefaultRecordingDevice()?.Id;
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

        private string GetInputDeviceLabel(string deviceId, bool includeActiveMarker, bool includeDefaultStar = false)
        {
            var device = SafeGetInputDevices().FirstOrDefault(a => string.Equals(a.Id, deviceId, StringComparison.OrdinalIgnoreCase));
            var name = GetInputDeviceDisplayName(deviceId);
            var text = FormatDeviceVisual(settings.GetInputIcon(deviceId), name);

            if (includeDefaultStar && device?.IsDefault == true)
            {
                text = "â˜… " + text;
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

        private string GetFullscreenInputDeviceMenuText(AudioDevice device, string currentDeviceId)
        {
            var name = GetInputDeviceDisplayName(device);

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
                case "volume":
                    return "VOL";
                case "volume-off":
                    return "MUTE";
                case "volume-x":
                    return "VX";
                case "headphones":
                    return "HP";
                case "headset":
                    return "HS";
                case "mic":
                    return "MIC";
                case "mic-off":
                    return "MIC-";
                case "mic-vocal":
                    return "VOC";
                case "webcam":
                    return "CAM";
                case "audio-lines":
                    return "EQ";
                case "audio-waveform":
                    return "WAV";
                case "podcast":
                    return "POD";
                case "radio":
                    return "RAD";
                case "radio-receiver":
                    return "REC";
                case "speaker":
                    return "SP";
                case "monitor-speaker":
                    return "MS";
                case "boom-box":
                    return "BOX";
                case "tv":
                    return "TV";
                case "monitor":
                    return "PC";
                case "laptop":
                    return "LAP";
                case "pc-case":
                    return "CASE";
                case "smartphone":
                    return "PH";
                case "tablet":
                    return "TAB";
                case "gamepad-2":
                    return "GP";
                case "bluetooth":
                    return "BT";
                case "bluetooth-connected":
                    return "BT+";
                case "bluetooth-searching":
                    return "BT?";
                case "usb":
                    return "USB";
                case "hdmi-port":
                    return "HDMI";
                case "cable":
                    return "CAB";
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

        private void ShowVolumeInfoMessage()
        {
            try
            {
                var state = AudioDevices.GetDefaultPlaybackVolume();
                ShowInfoMessage($"{Loc("LOCAS_VolumeTitle")}: {state.VolumePercent}%");
            }
            catch
            {
            }
        }

        private void ShowInputVolumeInfoMessage()
        {
            try
            {
                var state = AudioDevices.GetDefaultRecordingVolume();
                ShowInfoMessage($"{Loc("LOCAS_AudioInput")}: {state.VolumePercent}%");
            }
            catch
            {
            }
        }

        private void ShowGameProfileAppliedMessage(string gameName, IReadOnlyList<string> appliedParts)
        {
            if (!ShouldShowGameProfileNotifications())
            {
                return;
            }

            var title = string.IsNullOrWhiteSpace(gameName)
                ? Loc("LOCAS_GameProfileApplied")
                : $"{Loc("LOCAS_GameProfileApplied")}: {gameName}";
            var detail = appliedParts == null || appliedParts.Count == 0
                ? string.Empty
                : $" - {string.Join(" + ", appliedParts.Where(a => !string.IsNullOrWhiteSpace(a)))}";
            ShowMessage(title + detail);
        }

        private void ShowGameProfileInfoMessage(string message)
        {
            if (ShouldShowGameProfileNotifications())
            {
                ShowMessage(message);
            }
        }

        private void ShowMuteInfoMessage(string message)
        {
            if (ShouldShowMuteNotifications())
            {
                ShowMessage(message);
            }
        }

        private void ShowInfoMessage(string message)
        {
            if (settings.ShowNotifications)
            {
                ShowMessage(message);
            }
        }

        private bool ShouldShowOutputDeviceNotifications()
        {
            return settings.ShowNotifications && settings.ShowOutputDeviceNotifications;
        }

        private bool ShouldShowInputDeviceNotifications()
        {
            return settings.ShowNotifications && settings.ShowInputDeviceNotifications;
        }

        private bool ShouldShowVolumeNotifications()
        {
            return settings.ShowNotifications && settings.ShowVolumeNotifications;
        }

        private bool ShouldShowMuteNotifications()
        {
            return settings.ShowNotifications && settings.ShowMuteNotifications;
        }

        private bool ShouldShowGameProfileNotifications()
        {
            return settings.ShowNotifications && settings.ShowGameProfileNotifications;
        }

        private bool ShouldShowSpatialSoundNotifications()
        {
            return settings.ShowNotifications && settings.ShowSpatialSoundNotifications;
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
