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
using System.Threading.Tasks;
using Microsoft.Win32;
using Playnite.SDK;
using Playnite.SDK.Controls;
using Playnite.SDK.Data;
using Playnite.SDK.Events;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;

namespace PlayniteAudioSwitcher
{
    public sealed class AudioSwitcherPlugin : GenericPlugin
    {
        private readonly ILogger logger;
        private readonly HashSet<ControllerInput> pressedInputs = new HashSet<ControllerInput>();
        private readonly object mediaSourceSessionIdsLock = new object();
        private readonly Dictionary<Guid, AudioDevice> previousDevicesByGame = new Dictionary<Guid, AudioDevice>();
        private readonly Dictionary<Guid, AudioDevice> previousInputDevicesByGame = new Dictionary<Guid, AudioDevice>();
        private readonly Dictionary<Guid, HashSet<uint>> audioSessionBaselineByGame = new Dictionary<Guid, HashSet<uint>>();
        private AudioSwitcherSettings settings;
        private GameAudioProfileStore gameProfiles;
        private DateTime lastQuickSwitch = DateTime.MinValue;
        private ResourceDictionary englishFallbackResources;
        private Window activeThemeSelectorWindow;
        private AudioDeviceListControl activeThemeSelectorList;
        private Func<bool> isThemeSelectorOpen;
        private Action closeThemeSelector;
        private Guid? activeGameId;
        private int activeGameProcessId;
        private string activeGameName;
        private HashSet<uint> activeGameAudioSessionProcessIds = new HashSet<uint>();
        private string currentMediaSessionId;
        private readonly Dictionary<string, IReadOnlyList<string>> mediaSourceSessionIds = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        private DispatcherTimer mediaSessionDiscoveryTimer;
        private bool isMediaSessionDiscoveryRunning;
        private string lastMediaSessionDiscoverySignature;

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
                    "InputVolumeSlider",
                    "GameVolumeSlider",
                    "OutputWidget",
                    "InputWidget",
                    "GameVolumeWidget",
                    "MediaSessionList",
                    "MediaVolumeSlider",
                    "MediaWidget",
                    "MediaMixer"
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
                },
                new MainMenuItem
                {
                    MenuSection = $"{MenuRoot}|{Loc("LOCAS_MenuTools")}",
                    Description = Loc("LOCAS_AudioSessionDiagnostics"),
                    Action = _ => ExportAudioSessionDiagnostics()
                },
                new MainMenuItem
                {
                    MenuSection = $"{MenuRoot}|{Loc("LOCAS_MenuBackupRestore")}",
                    Description = Loc("LOCAS_ExportSettingsBackup"),
                    Action = _ => ExportSettingsBackup()
                },
                new MainMenuItem
                {
                    MenuSection = $"{MenuRoot}|{Loc("LOCAS_MenuBackupRestore")}",
                    Description = Loc("LOCAS_ImportSettingsBackup"),
                    Action = _ => ImportSettingsBackup()
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

            try
            {
                var game = games[0];
                var currentProfile = gameProfiles.GetProfile(game);
                var selectedDeviceId = string.IsNullOrWhiteSpace(currentProfile?.DeviceId)
                    ? SafeGetDefaultPlaybackDevice("building the game context menu")?.Id
                    : currentProfile.DeviceId;
                var inputDevices = SafeGetInputDevices().Where(a => a.IsVisible).ToList();
                var selectedInputDeviceId = string.IsNullOrWhiteSpace(currentProfile?.InputDeviceId)
                    ? SafeGetDefaultRecordingDevice("building the game context menu")?.Id
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

                if (inputDevices.Count == 0)
                {
                    items.Add(new GameMenuItem
                    {
                        MenuSection = $"{root}|{Loc("LOCAS_MenuChooseInput")}",
                        Description = Loc("LOCAS_NoRecordingDevicesAvailable"),
                        Action = _ => { }
                    });
                }

                foreach (var device in inputDevices)
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

                AddGameVolumeProfileMenuItems(items, root, game, currentProfile);

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
            catch (Exception ex)
            {
                logger.Error(ex, "Failed to build Audio Switcher game menu items.");
                return Enumerable.Empty<GameMenuItem>();
            }
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

            if (args.Name == "GameVolumeSlider")
            {
                return new AudioGameVolumeSliderControl(this);
            }

            if (args.Name == "OutputWidget")
            {
                return new AudioOutputWidgetControl(this);
            }

            if (args.Name == "InputWidget")
            {
                return new AudioInputWidgetControl(this);
            }

            if (args.Name == "GameVolumeWidget")
            {
                return new AudioGameVolumeWidgetControl(this);
            }

            if (args.Name == "MediaSessionList")
            {
                return new AudioMediaSessionListControl(this);
            }

            if (args.Name == "MediaVolumeSlider")
            {
                return new AudioMediaVolumeSliderControl(this);
            }

            if (args.Name == "MediaWidget")
            {
                return new AudioMediaWidgetControl(this);
            }

            if (args.Name == "MediaMixer")
            {
                return new AudioMediaMixerControl(this);
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
            StartMediaSessionDiscovery();
        }

        private void StartMediaSessionDiscovery()
        {
            if (mediaSessionDiscoveryTimer != null)
            {
                return;
            }

            mediaSessionDiscoveryTimer = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromMilliseconds(1500)
            };
            mediaSessionDiscoveryTimer.Tick += MediaSessionDiscoveryTimer_Tick;
            mediaSessionDiscoveryTimer.Start();
        }

        private async void MediaSessionDiscoveryTimer_Tick(object sender, EventArgs e)
        {
            if (isMediaSessionDiscoveryRunning)
            {
                return;
            }

            isMediaSessionDiscoveryRunning = true;
            try
            {
                var knownGameSessionProcessIds = new HashSet<uint>(activeGameAudioSessionProcessIds);
                var gameProcessId = activeGameProcessId;
                var sessions = await Task.Run(() => GetMediaAudioSessionsForDiscovery(knownGameSessionProcessIds, gameProcessId));
                if (Theme == null || Theme.IsMediaSessionVolumeWritePending)
                {
                    return;
                }

                var signature = string.Join("|", sessions.Select(a => a.Id).OrderBy(a => a, StringComparer.OrdinalIgnoreCase));
                if (!string.Equals(lastMediaSessionDiscoverySignature, signature, StringComparison.Ordinal))
                {
                    lastMediaSessionDiscoverySignature = signature;
                    logger.Debug($"Media session discovery updated: {sessions.Count} session group(s).");
                }

                Theme.RefreshMediaSessions(sessions);
            }
            catch (Exception ex)
            {
                logger.Warn(ex, "Failed to refresh media sessions in the background.");
            }
            finally
            {
                isMediaSessionDiscoveryRunning = false;
            }
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
                string.IsNullOrWhiteSpace(profile.SpatialSoundMode) &&
                !profile.GameVolumePercent.HasValue)
            {
                return;
            }

            try
            {
                var appliedParts = new List<string>();
                if (profile.GameVolumePercent.HasValue && args.Game != null)
                {
                    audioSessionBaselineByGame[args.Game.Id] = new HashSet<uint>(AudioDevices.GetPlaybackAudioSessionProcessIds());
                }

                if (!string.IsNullOrWhiteSpace(profile.DeviceId))
                {
                    var previousDevice = SafeGetDefaultPlaybackDevice("storing the previous playback device before applying a game profile");
                    if (previousDevice != null)
                    {
                        previousDevicesByGame[args.Game.Id] = previousDevice;
                    }

                    SetConfiguredDevice(profile.DeviceId, false);
                    appliedParts.Add(GetDeviceDisplayName(profile.DeviceId));
                }

                if (!string.IsNullOrWhiteSpace(profile.InputDeviceId))
                {
                    var previousInputDevice = SafeGetDefaultRecordingDevice("storing the previous recording device before applying a game profile");
                    if (previousInputDevice != null)
                    {
                        previousInputDevicesByGame[args.Game.Id] = previousInputDevice;
                    }

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

                if (appliedParts.Count > 0)
                {
                    ShowGameProfileAppliedMessage(args.Game?.Name, appliedParts);
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Failed to apply audio profile for {args.Game?.Name}.");
                ShowMessage($"{Loc("LOCAS_AudioProfileFailed")}: {args.Game?.Name}");
            }
        }

        public override void OnGameStarted(OnGameStartedEventArgs args)
        {
            activeGameId = args.Game?.Id;
            activeGameProcessId = args.StartedProcessId;
            activeGameName = args.Game?.Name;
            activeGameAudioSessionProcessIds = new HashSet<uint>();
            Theme?.Refresh();

            if (!settings.GameProfilesEnabled || args.Game == null || args.StartedProcessId <= 0)
            {
                if (args.Game != null && args.StartedProcessId > 0)
                {
                    ScheduleRefreshGameVolume(args.Game, args.StartedProcessId);
                }

                return;
            }

            var profile = gameProfiles.GetProfile(args.Game);
            if (profile?.GameVolumePercent == null)
            {
                ScheduleRefreshGameVolume(args.Game, args.StartedProcessId);
                return;
            }

            audioSessionBaselineByGame.TryGetValue(args.Game.Id, out var baselineProcessIds);
            ScheduleApplyGameVolume(args.Game, args.StartedProcessId, profile.GameVolumePercent.Value, baselineProcessIds);
        }

        public override void OnGameStopped(OnGameStoppedEventArgs args)
        {
            if (args.Game != null && activeGameId == args.Game.Id)
            {
                activeGameId = null;
                activeGameProcessId = 0;
                activeGameName = null;
                activeGameAudioSessionProcessIds.Clear();
                audioSessionBaselineByGame.Remove(args.Game.Id);
                Theme?.Refresh();
            }

            if (!settings.RestoreDeviceAfterGameProfile || args.Game == null)
            {
                return;
            }

            if (previousDevicesByGame.TryGetValue(args.Game.Id, out var previousDevice))
            {
                previousDevicesByGame.Remove(args.Game.Id);
                if (previousDevice != null)
                {
                    SetDevice(previousDevice.Id, GetDeviceDisplayName(previousDevice), false);
                }
            }

            if (previousInputDevicesByGame.TryGetValue(args.Game.Id, out var previousInputDevice))
            {
                previousInputDevicesByGame.Remove(args.Game.Id);
                if (previousInputDevice != null)
                {
                    SetInputDevice(previousInputDevice.Id, GetInputDeviceDisplayName(previousInputDevice), false);
                }
            }
        }

        private void ScheduleApplyGameVolume(Game game, int processId, int volumePercent, HashSet<uint> baselineProcessIds)
        {
            var gameId = game.Id;
            var gameName = game.Name;
            var normalizedVolume = Math.Max(0, Math.Min(100, volumePercent)) / 100f;
            var baseline = baselineProcessIds ?? new HashSet<uint>();

            Task.Run(async () =>
            {
                for (var attempt = 0; attempt < 40; attempt++)
                {
                    if (activeGameId != gameId || activeGameProcessId != processId)
                    {
                        return;
                    }

                    try
                    {
                        var targetProcessIds = GetTargetGameAudioSessionProcessIds(game, processId, baseline);
                        if (targetProcessIds.Count > 0 && AudioDevices.SetProcessVolumes(targetProcessIds, normalizedVolume))
                        {
                            Application.Current?.Dispatcher?.BeginInvoke(new Action(() =>
                            {
                                activeGameAudioSessionProcessIds = targetProcessIds;
                                Theme?.Refresh();
                                ShowGameProfileAppliedMessage(gameName, new[] { $"{Loc("LOCAS_GameVolumeTitle")} {volumePercent}%" });
                            }));
                            return;
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.Warn(ex, $"Failed to apply game session volume for {gameName}.");
                    }

                    await Task.Delay(750).ConfigureAwait(false);
                }

                logger.Info($"No active audio session was found for {gameName} after launch. Game volume profile was not applied.");
            });
        }

        private HashSet<uint> GetTargetGameAudioSessionProcessIds(Game game, int processId, HashSet<uint> baselineProcessIds)
        {
            var targetProcessIds = new HashSet<uint>(AudioDevices.GetProcessTreeAudioSessionProcessIds(processId));
            if (targetProcessIds.Count > 0)
            {
                return targetProcessIds;
            }

            foreach (var installProcessId in GetRunningGameInstallDirectoryProcessIds(game))
            {
                targetProcessIds.Add(installProcessId);
            }

            if (targetProcessIds.Count > 0)
            {
                return targetProcessIds;
            }

            var currentProcessId = (uint)Process.GetCurrentProcess().Id;
            var newSessionProcessIds = AudioDevices.GetPlaybackAudioSessionProcessIds()
                .Where(id => id != currentProcessId && (baselineProcessIds == null || !baselineProcessIds.Contains(id)))
                .ToList();

            if (newSessionProcessIds.Count == 1)
            {
                targetProcessIds.Add(newSessionProcessIds[0]);
            }
            else if (newSessionProcessIds.Count > 1)
            {
                logger.Info($"Multiple new audio sessions were detected after game launch: {string.Join(", ", newSessionProcessIds)}. Game volume profile was not applied automatically.");
            }

            return targetProcessIds;
        }

        private HashSet<uint> GetRunningGameInstallDirectoryProcessIds(Game game)
        {
            var processIds = new HashSet<uint>();
            var installDirectories = GetGameInstallDirectories(game).ToList();
            if (installDirectories.Count == 0)
            {
                return processIds;
            }

            var normalizedInstallDirectories = installDirectories
                .Where(Directory.Exists)
                .Select(path => Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var process in Process.GetProcesses())
            {
                try
                {
                    if (process.Id == Process.GetCurrentProcess().Id)
                    {
                        continue;
                    }

                    var modulePath = process.MainModule?.FileName;
                    if (string.IsNullOrWhiteSpace(modulePath))
                    {
                        continue;
                    }

                    var normalizedModulePath = Path.GetFullPath(modulePath);
                    if (normalizedInstallDirectories.Any(directory => normalizedModulePath.StartsWith(directory, StringComparison.OrdinalIgnoreCase)))
                    {
                        processIds.Add((uint)process.Id);
                    }
                }
                catch
                {
                    // Some system or protected processes do not expose MainModule. Ignore them.
                }
                finally
                {
                    process.Dispose();
                }
            }

            if (processIds.Count > 0)
            {
                logger.Info($"Detected running game processes from install directory for {game?.Name}: {string.Join(", ", processIds)}.");
            }

            return processIds;
        }

        private IEnumerable<string> GetGameInstallDirectories(Game game)
        {
            if (!string.IsNullOrWhiteSpace(game?.InstallDirectory))
            {
                yield return game.InstallDirectory;
            }

            var steamDirectory = TryResolveSteamAppInstallDirectory(game?.GameId);
            if (!string.IsNullOrWhiteSpace(steamDirectory))
            {
                yield return steamDirectory;
            }
        }

        private string TryResolveSteamAppInstallDirectory(string gameId)
        {
            if (string.IsNullOrWhiteSpace(gameId) || !gameId.All(char.IsDigit))
            {
                return null;
            }

            foreach (var library in GetSteamLibraryDirectories())
            {
                try
                {
                    var manifestPath = Path.Combine(library, "steamapps", $"appmanifest_{gameId}.acf");
                    if (!File.Exists(manifestPath))
                    {
                        continue;
                    }

                    var installDir = ReadSteamManifestValue(manifestPath, "installdir");
                    if (string.IsNullOrWhiteSpace(installDir))
                    {
                        continue;
                    }

                    var fullPath = Path.Combine(library, "steamapps", "common", installDir);
                    if (Directory.Exists(fullPath))
                    {
                        return fullPath;
                    }
                }
                catch (Exception ex)
                {
                    logger.Warn(ex, $"Failed to resolve Steam install directory for app {gameId}.");
                }
            }

            return null;
        }

        private IEnumerable<string> GetSteamLibraryDirectories()
        {
            var steamRoots = new List<string>();
            try
            {
                var steamPath = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam")?.GetValue("SteamPath")?.ToString();
                if (!string.IsNullOrWhiteSpace(steamPath))
                {
                    steamRoots.Add(steamPath.Replace('/', Path.DirectorySeparatorChar));
                }
            }
            catch
            {
            }

            steamRoots.Add(@"C:\Program Files (x86)\Steam");
            steamRoots.Add(@"C:\Program Files\Steam");

            foreach (var root in steamRoots.Where(Directory.Exists).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                yield return root;

                var libraryFoldersPath = Path.Combine(root, "steamapps", "libraryfolders.vdf");
                if (!File.Exists(libraryFoldersPath))
                {
                    continue;
                }

                foreach (var path in ReadSteamLibraryFolderPaths(libraryFoldersPath))
                {
                    if (Directory.Exists(path))
                    {
                        yield return path;
                    }
                }
            }
        }

        private static IEnumerable<string> ReadSteamLibraryFolderPaths(string libraryFoldersPath)
        {
            foreach (var line in File.ReadLines(libraryFoldersPath))
            {
                var trimmed = line.Trim();
                if (!trimmed.StartsWith("\"path\"", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var value = TryGetSecondQuotedValue(trimmed);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    yield return value.Replace(@"\\", @"\");
                }
            }
        }

        private static string ReadSteamManifestValue(string manifestPath, string key)
        {
            foreach (var line in File.ReadLines(manifestPath))
            {
                var trimmed = line.Trim();
                if (!trimmed.StartsWith($"\"{key}\"", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var value = TryGetSecondQuotedValue(trimmed);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }

            return null;
        }

        private static string TryGetSecondQuotedValue(string line)
        {
            var values = new List<string>();
            var index = 0;
            while (index < line.Length)
            {
                var start = line.IndexOf('"', index);
                if (start < 0)
                {
                    break;
                }

                var end = line.IndexOf('"', start + 1);
                if (end < 0)
                {
                    break;
                }

                values.Add(line.Substring(start + 1, end - start - 1));
                index = end + 1;
            }

            return values.Count >= 2 ? values[1] : null;
        }

        private void ScheduleRefreshGameVolume(Game game, int processId)
        {
            var gameId = game.Id;

            Task.Run(async () =>
            {
                for (var attempt = 0; attempt < 20; attempt++)
                {
                    if (activeGameId != gameId || activeGameProcessId != processId)
                    {
                        return;
                    }

                    try
                    {
                        if (AudioDevices.GetProcessTreeVolume(processId).IsAvailable)
                        {
                            Application.Current?.Dispatcher?.BeginInvoke(new Action(() => Theme?.Refresh()));
                            return;
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.Warn(ex, $"Failed to refresh game session volume for {game.Name}.");
                    }

                    await Task.Delay(500).ConfigureAwait(false);
                }
            });
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
            return GetThemeSelectorDevices(includeHidden, GetCurrentDeviceId());
        }

        internal IReadOnlyList<AudioDevice> GetThemeSelectorDevices(bool includeHidden, string currentDeviceId)
        {
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
            return GetThemeSelectorInputDevices(includeHidden, GetCurrentInputDeviceId());
        }

        internal IReadOnlyList<AudioDevice> GetThemeSelectorInputDevices(bool includeHidden, string currentDeviceId)
        {
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

        internal AudioDevice GetCurrentPlaybackDeviceForTheme()
        {
            return SafeGetDefaultPlaybackDevice("refreshing the theme playback state");
        }

        internal AudioDevice GetCurrentRecordingDeviceForTheme()
        {
            return SafeGetDefaultRecordingDevice("refreshing the theme recording state");
        }

        internal string GetDeviceDisplayNameForTheme(AudioDevice device)
        {
            return GetDeviceDisplayName(device);
        }

        internal string GetInputDeviceDisplayNameForTheme(AudioDevice device)
        {
            return GetInputDeviceDisplayName(device);
        }

        internal string GetDeviceDisplayLabelForTheme(AudioDevice device)
        {
            var name = GetDeviceDisplayName(device);
            var icon = settings.GetIcon(device?.Id);
            return FormatDeviceVisual(icon, name);
        }

        internal string GetInputDeviceDisplayLabelForTheme(AudioDevice device)
        {
            var name = GetInputDeviceDisplayName(device);
            var icon = settings.GetInputIcon(device?.Id);
            return FormatDeviceVisual(icon, name);
        }

        internal Geometry GetDeviceIconGeometryForTheme(AudioDevice device, bool input)
        {
            var icon = input ? settings.GetInputIcon(device?.Id) : settings.GetIcon(device?.Id);
            return string.IsNullOrWhiteSpace(icon) ? null : GetIconGeometry(icon);
        }

        public AudioVolumeState GetCurrentVolumeState()
        {
            return AudioDevices.GetDefaultPlaybackVolume();
        }

        public AudioVolumeState GetCurrentInputVolumeState()
        {
            return AudioDevices.GetDefaultRecordingVolume();
        }

        public AudioVolumeState GetCurrentGameVolumeState()
        {
            if (activeGameAudioSessionProcessIds.Count > 0)
            {
                return AudioDevices.GetProcessVolume(activeGameAudioSessionProcessIds);
            }

            if (activeGameProcessId <= 0)
            {
                return new AudioVolumeState { IsAvailable = false };
            }

            return AudioDevices.GetProcessTreeVolume(activeGameProcessId);
        }

        public string GetCurrentGameName()
        {
            return activeGameName;
        }

        public AudioSessionInfo GetCurrentGameSessionInfo()
        {
            try
            {
                if (activeGameAudioSessionProcessIds.Count > 0)
                {
                    return AudioDevices.GetFirstProcessAudioSession(activeGameAudioSessionProcessIds);
                }

                if (activeGameProcessId <= 0)
                {
                    return null;
                }

                var processIds = AudioDevices.GetProcessTreeAudioSessionProcessIds(activeGameProcessId);
                return AudioDevices.GetFirstProcessAudioSession(processIds);
            }
            catch (Exception ex)
            {
                logger.Warn(ex, $"Failed to read current game audio session information for {activeGameName ?? "current game"}.");
                return null;
            }
        }

        public IReadOnlyList<AudioSessionInfo> GetMediaAudioSessions()
        {
            return GetMediaAudioSessions(GetCurrentGameAudioSessionProcessIds());
        }

        private IReadOnlyList<AudioSessionInfo> GetMediaAudioSessionsForDiscovery(HashSet<uint> excludedProcessIds, int gameProcessId)
        {
            excludedProcessIds = excludedProcessIds ?? new HashSet<uint>();
            if (gameProcessId > 0)
            {
                try
                {
                    foreach (var processId in AudioDevices.GetProcessTreeAudioSessionProcessIds(gameProcessId))
                    {
                        excludedProcessIds.Add(processId);
                    }
                }
                catch (Exception ex)
                {
                    logger.Warn(ex, "Failed to exclude current game sessions during media session discovery.");
                }
            }

            return GetMediaAudioSessions(excludedProcessIds);
        }

        private IReadOnlyList<AudioSessionInfo> GetMediaAudioSessions(HashSet<uint> excludedProcessIds)
        {
            try
            {
                var sessions = AudioDevices.GetPlaybackAudioSessions()
                    .Where(session => !string.IsNullOrWhiteSpace(session.Id))
                    .Where(session => session.ProcessId != 0)
                    .Where(session => !excludedProcessIds.Contains(session.ProcessId))
                    .Where(session => !IsIgnoredMediaSession(session))
                    .Where(IsLikelyMediaSession)
                    .GroupBy(GetMediaSessionGroupKey)
                    .Select(CreateGroupedMediaSession)
                    .Where(session => session != null)
                    .OrderByDescending(GetMediaSessionPriority)
                    .ThenBy(session => GetMediaSessionDisplayName(session))
                    .ToList();
                UpdateMediaSourceSessionIds(sessions);
                return sessions;
            }
            catch (Exception ex)
            {
                logger.Warn(ex, "Failed to enumerate media audio sessions.");
                return new List<AudioSessionInfo>();
            }
        }

        public string GetCurrentMediaSessionId()
        {
            var sessions = GetMediaAudioSessions();
            return ResolveCurrentMediaSession(sessions)?.Id;
        }

        public AudioSessionInfo GetCurrentMediaSessionInfo()
        {
            return ResolveCurrentMediaSession(GetMediaAudioSessions());
        }

        public AudioVolumeState GetCurrentMediaSessionVolumeState()
        {
            var sessionId = GetCurrentMediaSessionId();
            return GetMediaSessionVolumeState(sessionId);
        }

        internal AudioSessionInfo ResolveCurrentMediaSession(IReadOnlyList<AudioSessionInfo> sessions)
        {
            if (sessions == null || sessions.Count == 0)
            {
                currentMediaSessionId = null;
                return null;
            }

            var current = sessions.FirstOrDefault(session =>
                string.Equals(session.Id, currentMediaSessionId, StringComparison.OrdinalIgnoreCase));
            if (current == null)
            {
                current = sessions[0];
                currentMediaSessionId = current.Id;
            }

            return current;
        }

        internal AudioVolumeState GetMediaSessionVolumeState(string sessionId)
        {
            return string.IsNullOrWhiteSpace(sessionId)
                ? new AudioVolumeState { IsAvailable = false }
                : AudioDevices.GetPlaybackAudioSessionVolume(GetMediaSourceSessionIds(sessionId));
        }

        public void SetThemeSelectedMediaSession(string sessionId)
        {
            if (string.IsNullOrWhiteSpace(sessionId))
            {
                return;
            }

            currentMediaSessionId = sessionId;
            Theme?.RefreshMediaSessions();
        }

        public string GetMediaSessionDisplayName(AudioSessionInfo session)
        {
            if (session == null)
            {
                return Loc("LOCAS_MediaSessionUnavailable");
            }

            if (!string.IsNullOrWhiteSpace(session.DisplayName))
            {
                return session.DisplayName;
            }

            if (!string.IsNullOrWhiteSpace(session.ProcessName))
            {
                return GetFriendlyProcessName(session.ProcessName);
            }

            return session.FriendlyName;
        }

        public void SetMediaSessionVolume(float volume)
        {
            SetMediaSessionVolume(volume, true);
        }

        public void SetMediaSessionVolume(float volume, bool notify)
        {
            var sessionId = GetCurrentMediaSessionId();
            if (string.IsNullOrWhiteSpace(sessionId))
            {
                return;
            }

            SetMediaSessionVolume(sessionId, volume, notify);
        }

        public void SetMediaSessionVolume(string sessionId, float volume, bool notify)
        {
            SetMediaSessionVolumeCore(sessionId, volume, notify, true);
        }

        internal void SetMediaSessionVolumeFromTheme(string sessionId, float volume)
        {
            SetMediaSessionVolumeCore(sessionId, volume, false, false);
        }

        private void SetMediaSessionVolumeCore(string sessionId, float volume, bool notify, bool refreshTheme)
        {
            if (string.IsNullOrWhiteSpace(sessionId))
            {
                return;
            }

            try
            {
                if (!AudioDevices.SetPlaybackAudioSessionVolume(GetMediaSourceSessionIds(sessionId), volume))
                {
                    logger.Info("No active media audio session found while setting volume.");
                    if (refreshTheme)
                    {
                        Theme?.RefreshMediaSessionState(sessionId);
                    }

                    return;
                }

                if (refreshTheme)
                {
                    Theme?.RefreshMediaSessionState(sessionId);
                }

                if (notify)
                {
                    var state = AudioDevices.GetPlaybackAudioSessionVolume(GetMediaSourceSessionIds(sessionId));
                    RecordThemeChange("media-volume", $"{Loc("LOCAS_MediaSessionTitle")}: {state.VolumePercent}%", Theme?.CurrentMediaSessionVolumeIconGeometry);
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Failed to set media audio session volume.");
                ShowMessage($"{Loc("LOCAS_MediaSessionVolumeFailed")}: {ex.Message}");
            }
        }

        public void ChangeMediaSessionVolumeByStep(int direction)
        {
            var sessionId = GetCurrentMediaSessionId();
            if (string.IsNullOrWhiteSpace(sessionId))
            {
                return;
            }

            try
            {
                var step = Math.Max(1, settings.VolumeStepPercent) / 100f;
                var sourceSessionIds = GetMediaSourceSessionIds(sessionId);
                var stateBefore = AudioDevices.GetPlaybackAudioSessionVolume(sourceSessionIds);
                if (!stateBefore.IsAvailable ||
                    !AudioDevices.SetPlaybackAudioSessionVolume(sourceSessionIds, stateBefore.Volume + step * Math.Sign(direction)))
                {
                    logger.Info("No active media audio session found while changing volume.");
                    Theme?.RefreshMediaSessionState(sessionId);
                    return;
                }

                Theme?.RefreshMediaSessionState(sessionId);
                var state = GetCurrentMediaSessionVolumeState();
                RecordThemeChange("media-volume", $"{Loc("LOCAS_MediaSessionTitle")}: {state.VolumePercent}%", Theme?.CurrentMediaSessionVolumeIconGeometry);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Failed to change media audio session volume.");
                ShowMessage($"{Loc("LOCAS_MediaSessionVolumeFailed")}: {ex.Message}");
            }
        }

        public void ToggleMediaSessionMute()
        {
            var sessionId = GetCurrentMediaSessionId();
            if (string.IsNullOrWhiteSpace(sessionId))
            {
                return;
            }

            ToggleMediaSessionMute(sessionId);
        }

        public void ToggleMediaSessionMute(string sessionId)
        {
            if (string.IsNullOrWhiteSpace(sessionId))
            {
                return;
            }

            try
            {
                var sourceSessionIds = GetMediaSourceSessionIds(sessionId);
                var stateBefore = AudioDevices.GetPlaybackAudioSessionVolume(sourceSessionIds);
                if (!stateBefore.IsAvailable || !AudioDevices.SetPlaybackAudioSessionMute(sourceSessionIds, !stateBefore.IsMuted))
                {
                    logger.Info("No active media audio session found while toggling mute.");
                    Theme?.RefreshMediaSessionState(sessionId);
                    return;
                }

                Theme?.RefreshMediaSessionState(sessionId);
                var state = AudioDevices.GetPlaybackAudioSessionVolume(sourceSessionIds);
                RecordThemeChange("media-mute", state.IsMuted ? Loc("LOCAS_MediaSessionMuted") : Loc("LOCAS_MediaSessionUnmuted"), Theme?.CurrentMediaSessionVolumeIconGeometry);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Failed to toggle media audio session mute.");
                ShowMessage($"{Loc("LOCAS_MediaSessionVolumeFailed")}: {ex.Message}");
            }
        }

        public void SetVolume(float volume)
        {
            SetVolume(volume, true);
        }

        public void SetVolume(float volume, bool notify)
        {
            SetVolumeCore(volume, notify, true);
        }

        internal void SetVolumeFromTheme(float volume)
        {
            SetVolumeCore(volume, false, false);
        }

        private void SetVolumeCore(float volume, bool notify, bool refreshTheme)
        {
            try
            {
                AudioDevices.SetDefaultPlaybackVolume(volume);
                if (refreshTheme)
                {
                    Theme?.RefreshOutputVolume();
                    RecordThemeChange("volume", $"{Loc("LOCAS_VolumeTitle")}: {GetCurrentVolumeState().VolumePercent}%", Theme?.CurrentOutputVolumeIconGeometry);
                }

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
            SetInputVolumeCore(volume, notify, true);
        }

        internal void SetInputVolumeFromTheme(float volume)
        {
            SetInputVolumeCore(volume, false, false);
        }

        private void SetInputVolumeCore(float volume, bool notify, bool refreshTheme)
        {
            try
            {
                AudioDevices.SetDefaultRecordingVolume(volume);
                if (refreshTheme)
                {
                    Theme?.RefreshInputVolume();
                    RecordThemeChange("input-volume", $"{Loc("LOCAS_AudioInput")}: {GetCurrentInputVolumeState().VolumePercent}%", Theme?.CurrentInputVolumeIconGeometry);
                }

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

        public void SetGameVolume(float volume)
        {
            SetGameVolume(volume, true);
        }

        public void SetGameVolume(float volume, bool notify)
        {
            SetGameVolumeCore(volume, notify, true);
        }

        internal void SetGameVolumeFromTheme(float volume)
        {
            SetGameVolumeCore(volume, false, false);
        }

        private void SetGameVolumeCore(float volume, bool notify, bool refreshTheme)
        {
            if (activeGameProcessId <= 0)
            {
                return;
            }

            try
            {
                var changed = activeGameAudioSessionProcessIds.Count > 0
                    ? AudioDevices.SetProcessVolumes(activeGameAudioSessionProcessIds, volume)
                    : AudioDevices.SetProcessTreeVolume(activeGameProcessId, volume);
                if (!changed)
                {
                    logger.Info($"No active game audio session found while setting volume for {activeGameName ?? "current game"}.");
                    if (refreshTheme)
                    {
                        Theme?.RefreshGameVolumeState();
                    }

                    return;
                }

                if (refreshTheme)
                {
                    Theme?.RefreshGameVolumeState();
                    RecordThemeChange("game-volume", $"{Loc("LOCAS_GameVolumeTitle")}: {GetCurrentGameVolumeState().VolumePercent}%", Theme?.CurrentGameVolumeIconGeometry);
                }

                if (notify && ShouldShowVolumeNotifications())
                {
                    ShowGameVolumeInfoMessage();
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Failed to set current game volume.");
                ShowMessage($"{Loc("LOCAS_GameVolumeFailed")}: {ex.Message}");
            }
        }

        public void ChangeVolumeByStep(int direction)
        {
            try
            {
                var step = Math.Max(1, settings.VolumeStepPercent) / 100f;
                AudioDevices.ChangeDefaultPlaybackVolume(step * Math.Sign(direction));
                Theme?.RefreshOutputVolume();
                RecordThemeChange("volume", $"{Loc("LOCAS_VolumeTitle")}: {GetCurrentVolumeState().VolumePercent}%", Theme?.CurrentOutputVolumeIconGeometry);
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
                Theme?.RefreshInputVolume();
                RecordThemeChange("input-volume", $"{Loc("LOCAS_AudioInput")}: {GetCurrentInputVolumeState().VolumePercent}%", Theme?.CurrentInputVolumeIconGeometry);
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

        public void ChangeGameVolumeByStep(int direction)
        {
            if (activeGameProcessId <= 0)
            {
                return;
            }

            try
            {
                var step = Math.Max(1, settings.VolumeStepPercent) / 100f;
                var changed = false;
                if (activeGameAudioSessionProcessIds.Count > 0)
                {
                    var state = AudioDevices.GetProcessVolume(activeGameAudioSessionProcessIds);
                    changed = state.IsAvailable && AudioDevices.SetProcessVolumes(activeGameAudioSessionProcessIds, state.Volume + step * Math.Sign(direction));
                }
                else
                {
                    changed = AudioDevices.ChangeProcessTreeVolume(activeGameProcessId, step * Math.Sign(direction));
                }

                if (!changed)
                {
                    logger.Info($"No active game audio session found while changing volume for {activeGameName ?? "current game"}.");
                    Theme?.RefreshGameVolumeState();
                    return;
                }

                Theme?.RefreshGameVolumeState();
                RecordThemeChange("game-volume", $"{Loc("LOCAS_GameVolumeTitle")}: {GetCurrentGameVolumeState().VolumePercent}%", Theme?.CurrentGameVolumeIconGeometry);
                if (ShouldShowVolumeNotifications())
                {
                    ShowGameVolumeInfoMessage();
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Failed to change current game volume.");
                ShowMessage($"{Loc("LOCAS_GameVolumeFailed")}: {ex.Message}");
            }
        }

        public void ToggleMute()
        {
            try
            {
                AudioDevices.ToggleDefaultPlaybackMute();
                Theme?.RefreshOutputVolume();
                var state = AudioDevices.GetDefaultPlaybackVolume();
                RecordThemeChange("mute", state.IsMuted ? Loc("LOCAS_Muted") : Loc("LOCAS_Unmuted"), Theme?.CurrentOutputVolumeIconGeometry);
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
                Theme?.RefreshInputVolume();
                var state = AudioDevices.GetDefaultRecordingVolume();
                RecordThemeChange("input-mute", state.IsMuted ? Loc("LOCAS_InputMuted") : Loc("LOCAS_InputUnmuted"), Theme?.CurrentInputVolumeIconGeometry);
                ShowMuteInfoMessage(state.IsMuted ? Loc("LOCAS_InputMuted") : Loc("LOCAS_InputUnmuted"));
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Failed to toggle default recording mute.");
                ShowMessage($"{Loc("LOCAS_InputVolumeFailed")}: {ex.Message}");
            }
        }

        public void ToggleGameMute()
        {
            if (activeGameProcessId <= 0)
            {
                return;
            }

            try
            {
                var changed = false;
                if (activeGameAudioSessionProcessIds.Count > 0)
                {
                    var gameState = AudioDevices.GetProcessVolume(activeGameAudioSessionProcessIds);
                    changed = gameState.IsAvailable && AudioDevices.SetProcessMutes(activeGameAudioSessionProcessIds, !gameState.IsMuted);
                }
                else
                {
                    changed = AudioDevices.ToggleProcessTreeMute(activeGameProcessId);
                }

                if (!changed)
                {
                    logger.Info($"No active game audio session found while toggling mute for {activeGameName ?? "current game"}.");
                    Theme?.RefreshGameVolumeState();
                    return;
                }

                Theme?.RefreshGameVolumeState();
                var currentGameState = GetCurrentGameVolumeState();
                RecordThemeChange("game-mute", currentGameState.IsMuted ? Loc("LOCAS_GameMuted") : Loc("LOCAS_GameUnmuted"), Theme?.CurrentGameVolumeIconGeometry);
                ShowMuteInfoMessage(currentGameState.IsMuted ? Loc("LOCAS_GameMuted") : Loc("LOCAS_GameUnmuted"));
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Failed to toggle current game mute.");
                ShowMessage($"{Loc("LOCAS_GameVolumeFailed")}: {ex.Message}");
            }
        }

        public void ApplyDefaultVolumeForCurrentDevice()
        {
            var currentDeviceId = GetCurrentDeviceId();
            if (!string.IsNullOrWhiteSpace(currentDeviceId))
            {
                TryApplyDefaultVolume(currentDeviceId);
                Theme?.RefreshOutputVolume();
            }
        }

        public void ApplyDefaultInputVolumeForCurrentDevice()
        {
            var currentDeviceId = GetCurrentInputDeviceId();
            if (!string.IsNullOrWhiteSpace(currentDeviceId))
            {
                TryApplyDefaultInputVolume(currentDeviceId);
                Theme?.RefreshInputVolume();
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

        private AudioDevice SafeGetDefaultPlaybackDevice(string context)
        {
            return SafeGetDefaultDevice(() => AudioDevices.GetDefaultPlaybackDevice(), "playback", context);
        }

        private AudioDevice SafeGetDefaultRecordingDevice(string context)
        {
            return SafeGetDefaultDevice(() => AudioDevices.GetDefaultRecordingDevice(), "recording", context);
        }

        private AudioDevice SafeGetDefaultDevice(Func<AudioDevice> getDefaultDevice, string deviceKind, string context)
        {
            try
            {
                var device = getDefaultDevice();
                if (device == null)
                {
                    logger.Info($"No default {deviceKind} audio device available while {context}.");
                }

                return device;
            }
            catch (Exception ex)
            {
                if (AudioDeviceManager.IsEndpointNotFoundException(ex))
                {
                    logger.Info($"No default {deviceKind} audio device available while {context}. HRESULT=0x80070490.");
                    return null;
                }

                logger.Error(ex, $"Failed to get default {deviceKind} audio device while {context}.");
                return null;
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
            var device = SafeGetDevices().FirstOrDefault(a => string.Equals(a.Id, deviceId, StringComparison.OrdinalIgnoreCase));
            if (device == null)
            {
                ShowMessage(Loc("LOCAS_ConfiguredDeviceInactive"));
                return;
            }

            SetDevice(device.Id, GetDeviceDisplayName(device), notify);
        }

        private void SetConfiguredInputDevice(string deviceId, bool notify)
        {
            var device = SafeGetInputDevices().FirstOrDefault(a => string.Equals(a.Id, deviceId, StringComparison.OrdinalIgnoreCase));
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
                RecordThemeChange("output-device", GetOutputNotificationText(deviceName), GetCurrentDeviceIconGeometry());
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
                RecordThemeChange("input-device", $"{Loc("LOCAS_AudioInput")}: {deviceName}", GetCurrentInputDeviceIconGeometry());
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

        private void RecordThemeChange(string changeType, string message, Geometry iconGeometry = null)
        {
            Theme?.RecordChange(changeType, message, iconGeometry);
        }

        private HashSet<uint> GetCurrentGameAudioSessionProcessIds()
        {
            var processIds = new HashSet<uint>(activeGameAudioSessionProcessIds);
            if (activeGameProcessId <= 0)
            {
                return processIds;
            }

            try
            {
                foreach (var processId in AudioDevices.GetProcessTreeAudioSessionProcessIds(activeGameProcessId))
                {
                    processIds.Add(processId);
                }
            }
            catch (Exception ex)
            {
                logger.Warn(ex, $"Failed to read game audio session process ids for {activeGameName ?? "current game"}.");
            }

            return processIds;
        }

        private static bool IsIgnoredMediaSession(AudioSessionInfo session)
        {
            var processName = session?.ProcessName;
            if (string.IsNullOrWhiteSpace(processName))
            {
                return true;
            }

            switch (processName.ToLowerInvariant())
            {
                case "audiodg":
                case "playnite":
                case "playnite.desktopapp":
                case "playnite.fullscreenapp":
                case "steam":
                case "steamwebhelper":
                case "xboxpcapp":
                case "gamebar":
                case "gamebarftserver":
                case "gamingservices":
                case "gamingservicesnet":
                case "applicationframehost":
                case "shellexperiencehost":
                case "startmenuexperiencehost":
                case "explorer":
                    return true;
                default:
                    return false;
            }
        }

        private IReadOnlyList<string> GetMediaSourceSessionIds(string mediaSessionId)
        {
            if (string.IsNullOrWhiteSpace(mediaSessionId))
            {
                return new List<string>();
            }

            lock (mediaSourceSessionIdsLock)
            {
                if (mediaSourceSessionIds.TryGetValue(mediaSessionId, out var cachedIds) && cachedIds.Count > 0)
                {
                    return cachedIds.ToList();
                }
            }

            var session = GetMediaAudioSessions()
                .FirstOrDefault(a => string.Equals(a.Id, mediaSessionId, StringComparison.OrdinalIgnoreCase));
            if (session?.SourceSessionIds != null && session.SourceSessionIds.Count > 0)
            {
                return session.SourceSessionIds;
            }

            return new List<string> { mediaSessionId };
        }

        private void UpdateMediaSourceSessionIds(IEnumerable<AudioSessionInfo> sessions)
        {
            lock (mediaSourceSessionIdsLock)
            {
                mediaSourceSessionIds.Clear();
                foreach (var session in sessions ?? Enumerable.Empty<AudioSessionInfo>())
                {
                    if (string.IsNullOrWhiteSpace(session?.Id))
                    {
                        continue;
                    }

                    mediaSourceSessionIds[session.Id] = session.SourceSessionIds != null && session.SourceSessionIds.Count > 0
                        ? session.SourceSessionIds.ToList()
                        : new List<string> { session.Id };
                }
            }
        }

        private static string GetMediaSessionGroupKey(AudioSessionInfo session)
        {
            var processName = session?.ProcessName;
            if (!string.IsNullOrWhiteSpace(processName))
            {
                return $"process:{processName.ToLowerInvariant()}";
            }

            if (!string.IsNullOrWhiteSpace(session?.ProcessPath))
            {
                return $"path:{session.ProcessPath.ToLowerInvariant()}";
            }

            return session?.Id ?? string.Empty;
        }

        private static AudioSessionInfo CreateGroupedMediaSession(IGrouping<string, AudioSessionInfo> group)
        {
            var sessions = group
                .Where(a => a != null)
                .OrderByDescending(a => a.IsActive)
                .ThenBy(a => string.IsNullOrWhiteSpace(a.DisplayName) ? 1 : 0)
                .ThenBy(a => a.ProcessName)
                .ToList();
            var primary = sessions.FirstOrDefault();
            if (primary == null)
            {
                return null;
            }

            var levels = sessions.Select(a => a.Volume).ToList();
            var averageVolume = levels.Count > 0 ? levels.Average() : primary.Volume;
            var sourceIds = sessions
                .Select(a => a.Id)
                .Where(a => !string.IsNullOrWhiteSpace(a))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            return new AudioSessionInfo
            {
                Id = group.Key,
                ProcessId = primary.ProcessId,
                ProcessName = primary.ProcessName,
                ProcessPath = primary.ProcessPath,
                DisplayName = !string.IsNullOrWhiteSpace(primary.ProcessName)
                    ? GetFriendlyProcessName(primary.ProcessName)
                    : primary.DisplayName,
                IconPath = sessions.Select(a => a.IconPath).FirstOrDefault(a => !string.IsNullOrWhiteSpace(a)),
                SessionIdentifier = group.Key,
                SourceSessionIds = sourceIds,
                State = sessions.Any(a => a.IsActive) ? 1 : primary.State,
                Volume = Math.Max(0f, Math.Min(1f, averageVolume)),
                VolumePercent = (int)Math.Round(Math.Max(0f, Math.Min(1f, averageVolume)) * 100),
                IsMuted = sessions.Count > 0 && sessions.All(a => a.IsMuted)
            };
        }

        private static bool IsLikelyMediaSession(AudioSessionInfo session)
        {
            if (session == null)
            {
                return false;
            }

            var processName = session.ProcessName?.ToLowerInvariant() ?? string.Empty;
            var displayName = session.DisplayName?.ToLowerInvariant() ?? string.Empty;
            var iconPath = session.IconPath?.ToLowerInvariant() ?? string.Empty;
            var text = $"{processName} {displayName} {iconPath}";

            if (IsKnownMediaProcess(processName) ||
                text.Contains("spotify") ||
                text.Contains("youtube") ||
                text.Contains("music") ||
                text.Contains("media") ||
                text.Contains("uniplaysong"))
            {
                return true;
            }

            return session.IsActive && IsBrowserProcess(processName);
        }

        private static bool IsKnownMediaProcess(string processName)
        {
            if (string.IsNullOrWhiteSpace(processName))
            {
                return false;
            }

            switch (processName.ToLowerInvariant())
            {
                case "spotify":
                case "uniplaysong":
                case "vlc":
                case "wmplayer":
                case "music.ui":
                case "musicbee":
                case "foobar2000":
                case "aimp":
                case "itunes":
                case "winamp":
                case "plexamp":
                case "tidal":
                case "deezer":
                case "amazon music":
                case "soundcloud":
                    return true;
                default:
                    return false;
            }
        }

        private static bool IsBrowserProcess(string processName)
        {
            if (string.IsNullOrWhiteSpace(processName))
            {
                return false;
            }

            switch (processName.ToLowerInvariant())
            {
                case "chrome":
                case "msedge":
                case "firefox":
                case "brave":
                case "opera":
                case "opera_gx":
                case "vivaldi":
                    return true;
                default:
                    return false;
            }
        }

        private static int GetMediaSessionPriority(AudioSessionInfo session)
        {
            var processName = session?.ProcessName?.ToLowerInvariant() ?? string.Empty;
            if (IsKnownMediaProcess(processName))
            {
                return 30;
            }

            if (IsBrowserProcess(processName))
            {
                return 20;
            }

            return 10;
        }

        private static string GetFriendlyProcessName(string processName)
        {
            if (string.IsNullOrWhiteSpace(processName))
            {
                return string.Empty;
            }

            switch (processName.ToLowerInvariant())
            {
                case "chrome":
                    return "Google Chrome";
                case "msedge":
                    return "Microsoft Edge";
                case "firefox":
                    return "Firefox";
                case "brave":
                    return "Brave";
                case "spotify":
                    return "Spotify";
                default:
                    return processName;
            }
        }

        public void ExportAudioSessionDiagnostics()
        {
            try
            {
                var sessions = AudioDevices.GetPlaybackAudioSessions();
                var path = Path.Combine(GetPluginUserDataPath(), "audio-session-diagnostics.txt");
                using (var writer = new StreamWriter(path, false))
                {
                    writer.WriteLine("Audio Switcher - Windows audio session diagnostics");
                    writer.WriteLine($"Created: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                    writer.WriteLine($"Session count: {sessions.Count}");
                    writer.WriteLine();

                    if (sessions.Count == 0)
                    {
                        writer.WriteLine("No active playback audio sessions were reported by Windows.");
                    }

                    foreach (var session in sessions.OrderBy(a => a.ProcessName).ThenBy(a => a.ProcessId))
                    {
                        writer.WriteLine($"PID: {session.ProcessId}");
                        writer.WriteLine($"Process: {session.ProcessName ?? string.Empty}");
                        writer.WriteLine($"Path: {session.ProcessPath ?? string.Empty}");
                        writer.WriteLine($"Display name: {session.DisplayName ?? string.Empty}");
                        writer.WriteLine($"Icon path: {session.IconPath ?? string.Empty}");
                        writer.WriteLine($"Session id: {session.SessionIdentifier ?? string.Empty}");
                        writer.WriteLine($"Volume: {session.VolumePercent}%");
                        writer.WriteLine($"Muted: {session.IsMuted}");
                        writer.WriteLine();
                    }
                }

                logger.Info($"Audio session diagnostics exported to {path}.");
                if (ShouldShowDiagnosticNotifications())
                {
                    ShowMessage($"{Loc("LOCAS_AudioSessionDiagnosticsSaved")}: {path}");
                }

                if (PlayniteApi.ApplicationInfo.Mode == ApplicationMode.Desktop)
                {
                    Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Failed to export audio session diagnostics.");
                ShowMessage($"{Loc("LOCAS_AudioSessionDiagnosticsFailed")}: {ex.Message}");
            }
        }

        public void ExportSettingsBackup()
        {
            try
            {
                var dialog = new SaveFileDialog
                {
                    Title = Loc("LOCAS_ExportSettingsBackup"),
                    Filter = "JSON (*.json)|*.json",
                    FileName = $"AudioSwitcherBackup_{DateTime.Now:yyyyMMdd_HHmmss}.json"
                };

                if (dialog.ShowDialog() != true)
                {
                    return;
                }

                var backup = new AudioSwitcherBackup
                {
                    Settings = settings.GetSerializableClone(),
                    GameProfiles = gameProfiles.GetProfilesSnapshot()
                };

                File.WriteAllText(dialog.FileName, Serialization.ToJson(backup, true));
                logger.Info($"Audio Switcher settings backup exported to {dialog.FileName}.");
                ShowMessage(Loc("LOCAS_BackupExported"));
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Failed to export Audio Switcher settings backup.");
                ShowMessage($"{Loc("LOCAS_BackupExportFailed")}: {ex.Message}");
            }
        }

        public void ImportSettingsBackup()
        {
            try
            {
                var dialog = new OpenFileDialog
                {
                    Title = Loc("LOCAS_ImportSettingsBackup"),
                    Filter = "JSON (*.json)|*.json"
                };

                if (dialog.ShowDialog() != true)
                {
                    return;
                }

                if (!Serialization.TryFromJsonFile<AudioSwitcherBackup>(dialog.FileName, out var backup) ||
                    backup == null ||
                    backup.Settings == null)
                {
                    ShowMessage(Loc("LOCAS_BackupImportInvalid"));
                    return;
                }

                SavePluginSettings(backup.Settings);
                gameProfiles.ReplaceProfiles(backup.GameProfiles);
                ReloadSettings();
                ApplyDefaultVolumeForCurrentDevice();
                ApplyDefaultInputVolumeForCurrentDevice();
                Theme?.Refresh();

                logger.Info($"Audio Switcher settings backup imported from {dialog.FileName}.");
                ShowMessage(Loc("LOCAS_BackupImported"));
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Failed to import Audio Switcher settings backup.");
                ShowMessage($"{Loc("LOCAS_BackupImportFailed")}: {ex.Message}");
            }
        }

        private void AddGameVolumeProfileMenuItems(List<GameMenuItem> items, string root, Game game, GameAudioProfile currentProfile)
        {
            var section = $"{root}|{Loc("LOCAS_GameVolumeTitle")}";
            var selectedPercent = currentProfile?.GameVolumePercent;
            items.Add(new GameMenuItem
            {
                MenuSection = section,
                Description = GetCheckedMenuText(Loc("LOCAS_GameVolumeDefault"), !selectedPercent.HasValue),
                Action = _ =>
                {
                    gameProfiles.SetGameVolumePercent(game, null);
                    ShowGameProfileInfoMessage($"{game.Name}: {Loc("LOCAS_GameVolumeDefault")}");
                }
            });

            foreach (var percent in new[] { 0, 10, 20, 30, 40, 50, 60, 70, 80, 90, 100 })
            {
                var value = percent;
                var text = $"{value}%";
                items.Add(new GameMenuItem
                {
                    MenuSection = section,
                    Description = GetCheckedMenuText(text, selectedPercent == value),
                    Action = _ =>
                    {
                        gameProfiles.SetGameVolumePercent(game, value);
                        ShowGameProfileInfoMessage($"{game.Name}: {Loc("LOCAS_GameVolumeTitle")} {text}");
                    }
                });
            }
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
            if (device == null)
            {
                return Loc("LOCAS_UnknownDevice");
            }

            return settings.GetCustomName(device.Id) ?? device.Name ?? Loc("LOCAS_UnknownDevice");
        }

        private string GetDeviceDisplayName(string deviceId)
        {
            return settings.GetCustomName(deviceId) ??
                SafeGetDevices().FirstOrDefault(a => string.Equals(a.Id, deviceId, StringComparison.OrdinalIgnoreCase))?.Name ??
                Loc("LOCAS_UnknownDevice");
        }

        private string GetInputDeviceDisplayName(AudioDevice device)
        {
            if (device == null)
            {
                return Loc("LOCAS_UnknownDevice");
            }

            return settings.GetInputCustomName(device.Id) ?? device.Name ?? Loc("LOCAS_UnknownDevice");
        }

        private string GetInputDeviceDisplayName(string deviceId)
        {
            return settings.GetInputCustomName(deviceId) ??
                SafeGetInputDevices().FirstOrDefault(a => string.Equals(a.Id, deviceId, StringComparison.OrdinalIgnoreCase))?.Name ??
                Loc("LOCAS_UnknownDevice");
        }

        public string GetCurrentDeviceId()
        {
            return SafeGetDefaultPlaybackDevice("reading the current playback device")?.Id;
        }

        public string GetCurrentInputDeviceId()
        {
            return SafeGetDefaultRecordingDevice("reading the current recording device")?.Id;
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

        private void ShowGameVolumeInfoMessage()
        {
            try
            {
                var state = GetCurrentGameVolumeState();
                if (!state.IsAvailable)
                {
                    return;
                }

                var prefix = string.IsNullOrWhiteSpace(activeGameName)
                    ? Loc("LOCAS_GameVolumeTitle")
                    : $"{Loc("LOCAS_GameVolumeTitle")}: {activeGameName}";
                ShowInfoMessage($"{prefix}: {state.VolumePercent}%");
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

        private bool ShouldShowDiagnosticNotifications()
        {
            return settings.ShowNotifications && settings.ShowDiagnosticNotifications;
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
