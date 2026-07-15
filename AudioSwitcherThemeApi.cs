using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows.Input;
using System.Windows.Media;
using Playnite.SDK;
using Playnite.SDK.Data;

namespace PlayniteAudioSwitcher
{
    public sealed class AudioSwitcherThemeApi : ObservableObject
    {
        private readonly AudioSwitcherPlugin plugin;
        private bool isSelectorOpen;
        private string currentDeviceId;
        private string currentDeviceName;
        private string currentDeviceLabel;
        private Geometry currentDeviceIconGeometry;
        private bool hasDevices;
        private string currentInputDeviceId;
        private string currentInputDeviceName;
        private string currentInputDeviceLabel;
        private Geometry currentInputDeviceIconGeometry;
        private bool hasInputDevices;
        private float currentVolume;
        private int currentVolumePercent;
        private string currentVolumeLabel;
        private bool isMuted;
        private bool isOutputMuted;
        private Geometry currentOutputVolumeIconGeometry;
        private float currentInputVolume;
        private int currentInputVolumePercent;
        private string currentInputVolumeLabel;
        private bool isInputMuted;
        private Geometry currentInputVolumeIconGeometry;
        private float currentGameVolume;
        private int currentGameVolumePercent;
        private string currentGameVolumeLabel;
        private bool isGameMuted;
        private Geometry currentGameVolumeIconGeometry;
        private bool hasActiveGameAudioSession;
        private string gameSessionStatusLabel;
        private string currentGameName;
        private string currentGameProcessName;
        private string currentGameProcessPath;
        private string currentGameSessionName;
        private string currentGameSessionIconPath;
        private string currentMediaSessionId;
        private string currentMediaSessionName;
        private string currentMediaSessionProcessName;
        private string currentMediaSessionProcessPath;
        private string currentMediaSessionIconPath;
        private float currentMediaSessionVolume;
        private int currentMediaSessionVolumePercent;
        private string currentMediaSessionVolumeLabel;
        private bool isCurrentMediaSessionMuted;
        private bool hasMediaSessions;
        private bool hasSelectedMediaSession;
        private Geometry currentMediaSessionVolumeIconGeometry;
        private string lastChangeType;
        private string lastChangeMessage;
        private DateTime lastChangeAt;
        private Geometry lastChangeIconGeometry;
        private int volumeStepPercent;
        private bool isRefreshingVolume;
        private bool isRefreshingInputVolume;
        private bool isRefreshingGameVolume;
        private bool isRefreshingMediaSessionVolume;
        private int highlightedDeviceIndex = -1;
        private DateTime confirmAvailableAt = DateTime.MinValue;
        private readonly DeferredVolumeWriter outputVolumeWriter;
        private readonly DeferredVolumeWriter inputVolumeWriter;
        private readonly DeferredVolumeWriter gameVolumeWriter;
        private readonly DeferredKeyedVolumeWriter mediaVolumeWriter;

        public AudioSwitcherThemeApi(AudioSwitcherPlugin plugin)
        {
            this.plugin = plugin;
            outputVolumeWriter = new DeferredVolumeWriter(plugin.SetVolumeFromTheme, SynchronizeOutputVolume);
            inputVolumeWriter = new DeferredVolumeWriter(plugin.SetInputVolumeFromTheme, SynchronizeInputVolume);
            gameVolumeWriter = new DeferredVolumeWriter(plugin.SetGameVolumeFromTheme, SynchronizeGameVolume);
            mediaVolumeWriter = new DeferredKeyedVolumeWriter(plugin.SetMediaSessionVolumeFromTheme, RefreshMediaSessionState);
            Devices = new ObservableCollection<AudioSwitcherThemeDevice>();
            AllDevices = new ObservableCollection<AudioSwitcherThemeDevice>();
            InputDevices = new ObservableCollection<AudioSwitcherThemeDevice>();
            AllInputDevices = new ObservableCollection<AudioSwitcherThemeDevice>();
            MediaSessions = new ObservableCollection<AudioSwitcherThemeMediaSession>();
            ToggleSelectorCommand = new RelayCommand(ToggleSelector);
            OpenSelectorCommand = new RelayCommand(OpenSelector);
            CloseSelectorCommand = new RelayCommand(CloseSelector);
            NextDeviceCommand = new RelayCommand(() =>
            {
                plugin.ToggleCustomDevices();
                Refresh();
            });
            RefreshDevicesCommand = new RelayCommand(() =>
            {
                plugin.Settings.RefreshDevices();
                Refresh();
            });
            SetDeviceCommand = new RelayCommand<object>(SetDevice);
            VolumeUpCommand = new RelayCommand(() => ChangeVolume(1));
            VolumeDownCommand = new RelayCommand(() => ChangeVolume(-1));
            ToggleMuteCommand = new RelayCommand(ToggleMute);
            RefreshVolumeCommand = new RelayCommand(RefreshVolume);
            SetVolumeCommand = new RelayCommand<object>(SetVolume);
            RefreshInputDevicesCommand = new RelayCommand(() =>
            {
                plugin.Settings.RefreshDevices();
                Refresh();
            });
            SetInputDeviceCommand = new RelayCommand<object>(SetInputDevice);
            InputVolumeUpCommand = new RelayCommand(() => ChangeInputVolume(1));
            InputVolumeDownCommand = new RelayCommand(() => ChangeInputVolume(-1));
            ToggleInputMuteCommand = new RelayCommand(ToggleInputMute);
            RefreshInputVolumeCommand = new RelayCommand(RefreshInputVolume);
            SetInputVolumeCommand = new RelayCommand<object>(SetInputVolume);
            GameVolumeUpCommand = new RelayCommand(() => ChangeGameVolume(1));
            GameVolumeDownCommand = new RelayCommand(() => ChangeGameVolume(-1));
            ToggleGameMuteCommand = new RelayCommand(ToggleGameMute);
            RefreshGameVolumeCommand = new RelayCommand(RefreshGameVolume);
            SetGameVolumeCommand = new RelayCommand<object>(SetGameVolume);
            RefreshMediaSessionsCommand = new RelayCommand(RefreshMediaSessions);
            SetMediaSessionCommand = new RelayCommand<object>(SetMediaSession);
            MediaSessionVolumeUpCommand = new RelayCommand<object>(parameter => ChangeMediaSessionVolume(parameter, 1));
            MediaSessionVolumeDownCommand = new RelayCommand<object>(parameter => ChangeMediaSessionVolume(parameter, -1));
            ToggleMediaSessionMuteCommand = new RelayCommand<object>(ToggleMediaSessionMute);
            SetMediaSessionVolumeCommand = new RelayCommand<object>(SetMediaSessionVolume);
        }

        public ObservableCollection<AudioSwitcherThemeDevice> Devices { get; }

        public ObservableCollection<AudioSwitcherThemeDevice> AllDevices { get; }

        public ObservableCollection<AudioSwitcherThemeDevice> InputDevices { get; }

        public ObservableCollection<AudioSwitcherThemeDevice> AllInputDevices { get; }

        public ObservableCollection<AudioSwitcherThemeMediaSession> MediaSessions { get; }

        internal bool IsMediaSessionVolumeWritePending => mediaVolumeWriter.HasPendingWork;

        public ICommand ToggleSelectorCommand { get; }

        public ICommand OpenSelectorCommand { get; }

        public ICommand CloseSelectorCommand { get; }

        public ICommand NextDeviceCommand { get; }

        public ICommand RefreshDevicesCommand { get; }

        public ICommand SetDeviceCommand { get; }

        public ICommand VolumeUpCommand { get; }

        public ICommand VolumeDownCommand { get; }

        public ICommand ToggleMuteCommand { get; }

        public ICommand RefreshVolumeCommand { get; }

        public ICommand SetVolumeCommand { get; }

        public ICommand RefreshInputDevicesCommand { get; }

        public ICommand SetInputDeviceCommand { get; }

        public ICommand InputVolumeUpCommand { get; }

        public ICommand InputVolumeDownCommand { get; }

        public ICommand ToggleInputMuteCommand { get; }

        public ICommand RefreshInputVolumeCommand { get; }

        public ICommand SetInputVolumeCommand { get; }

        public ICommand GameVolumeUpCommand { get; }

        public ICommand GameVolumeDownCommand { get; }

        public ICommand ToggleGameMuteCommand { get; }

        public ICommand RefreshGameVolumeCommand { get; }

        public ICommand SetGameVolumeCommand { get; }

        public ICommand RefreshMediaSessionsCommand { get; }

        public ICommand SetMediaSessionCommand { get; }

        public ICommand MediaSessionVolumeUpCommand { get; }

        public ICommand MediaSessionVolumeDownCommand { get; }

        public ICommand ToggleMediaSessionMuteCommand { get; }

        public ICommand SetMediaSessionVolumeCommand { get; }

        public bool IsSelectorOpen
        {
            get => isSelectorOpen;
            set => SetValue(ref isSelectorOpen, value);
        }

        public string CurrentDeviceId
        {
            get => currentDeviceId;
            private set => SetValue(ref currentDeviceId, value);
        }

        public string CurrentDeviceName
        {
            get => currentDeviceName;
            private set => SetValue(ref currentDeviceName, value);
        }

        public string CurrentDeviceLabel
        {
            get => currentDeviceLabel;
            private set => SetValue(ref currentDeviceLabel, value);
        }

        public Geometry CurrentDeviceIconGeometry
        {
            get => currentDeviceIconGeometry;
            private set => SetValue(ref currentDeviceIconGeometry, value);
        }

        public bool HasDevices
        {
            get => hasDevices;
            private set => SetValue(ref hasDevices, value);
        }

        public string CurrentInputDeviceId
        {
            get => currentInputDeviceId;
            private set => SetValue(ref currentInputDeviceId, value);
        }

        public string CurrentInputDeviceName
        {
            get => currentInputDeviceName;
            private set => SetValue(ref currentInputDeviceName, value);
        }

        public string CurrentInputDeviceLabel
        {
            get => currentInputDeviceLabel;
            private set => SetValue(ref currentInputDeviceLabel, value);
        }

        public Geometry CurrentInputDeviceIconGeometry
        {
            get => currentInputDeviceIconGeometry;
            private set => SetValue(ref currentInputDeviceIconGeometry, value);
        }

        public bool HasInputDevices
        {
            get => hasInputDevices;
            private set => SetValue(ref hasInputDevices, value);
        }

        public float CurrentVolume
        {
            get => currentVolume;
            set
            {
                var normalized = Math.Max(0f, Math.Min(1f, value));
                if (isRefreshingVolume)
                {
                    SetValue(ref currentVolume, normalized);
                    return;
                }

                QueueOutputVolume(normalized);
            }
        }

        public int CurrentVolumePercent
        {
            get => currentVolumePercent;
            set
            {
                var normalized = Math.Max(0, Math.Min(100, value));
                if (isRefreshingVolume)
                {
                    SetValue(ref currentVolumePercent, normalized);
                    return;
                }

                QueueOutputVolume(normalized / 100f);
            }
        }

        public string CurrentVolumeLabel
        {
            get => currentVolumeLabel;
            private set => SetValue(ref currentVolumeLabel, value);
        }

        public bool IsMuted
        {
            get => isMuted;
            private set => SetValue(ref isMuted, value);
        }

        public bool IsOutputMuted
        {
            get => isOutputMuted;
            private set => SetValue(ref isOutputMuted, value);
        }

        public Geometry CurrentOutputVolumeIconGeometry
        {
            get => currentOutputVolumeIconGeometry;
            private set => SetValue(ref currentOutputVolumeIconGeometry, value);
        }

        public float CurrentInputVolume
        {
            get => currentInputVolume;
            set
            {
                var normalized = Math.Max(0f, Math.Min(1f, value));
                if (isRefreshingInputVolume)
                {
                    SetValue(ref currentInputVolume, normalized);
                    return;
                }

                QueueInputVolume(normalized);
            }
        }

        public int CurrentInputVolumePercent
        {
            get => currentInputVolumePercent;
            set
            {
                var normalized = Math.Max(0, Math.Min(100, value));
                if (isRefreshingInputVolume)
                {
                    SetValue(ref currentInputVolumePercent, normalized);
                    return;
                }

                QueueInputVolume(normalized / 100f);
            }
        }

        public string CurrentInputVolumeLabel
        {
            get => currentInputVolumeLabel;
            private set => SetValue(ref currentInputVolumeLabel, value);
        }

        public bool IsInputMuted
        {
            get => isInputMuted;
            private set => SetValue(ref isInputMuted, value);
        }

        public Geometry CurrentInputVolumeIconGeometry
        {
            get => currentInputVolumeIconGeometry;
            private set => SetValue(ref currentInputVolumeIconGeometry, value);
        }

        public float CurrentGameVolume
        {
            get => currentGameVolume;
            set
            {
                var normalized = Math.Max(0f, Math.Min(1f, value));
                if (isRefreshingGameVolume)
                {
                    SetValue(ref currentGameVolume, normalized);
                    return;
                }

                QueueGameVolume(normalized);
            }
        }

        public int CurrentGameVolumePercent
        {
            get => currentGameVolumePercent;
            set
            {
                var normalized = Math.Max(0, Math.Min(100, value));
                if (isRefreshingGameVolume)
                {
                    SetValue(ref currentGameVolumePercent, normalized);
                    return;
                }

                QueueGameVolume(normalized / 100f);
            }
        }

        public string CurrentGameVolumeLabel
        {
            get => currentGameVolumeLabel;
            private set => SetValue(ref currentGameVolumeLabel, value);
        }

        public bool IsGameMuted
        {
            get => isGameMuted;
            private set => SetValue(ref isGameMuted, value);
        }

        public Geometry CurrentGameVolumeIconGeometry
        {
            get => currentGameVolumeIconGeometry;
            private set => SetValue(ref currentGameVolumeIconGeometry, value);
        }

        public bool HasActiveGameAudioSession
        {
            get => hasActiveGameAudioSession;
            private set => SetValue(ref hasActiveGameAudioSession, value);
        }

        public string GameSessionStatusLabel
        {
            get => gameSessionStatusLabel;
            private set => SetValue(ref gameSessionStatusLabel, value);
        }

        public string CurrentGameName
        {
            get => currentGameName;
            private set => SetValue(ref currentGameName, value);
        }

        public string CurrentGameProcessName
        {
            get => currentGameProcessName;
            private set => SetValue(ref currentGameProcessName, value);
        }

        public string CurrentGameProcessPath
        {
            get => currentGameProcessPath;
            private set => SetValue(ref currentGameProcessPath, value);
        }

        public string CurrentGameSessionName
        {
            get => currentGameSessionName;
            private set => SetValue(ref currentGameSessionName, value);
        }

        public string CurrentGameSessionIconPath
        {
            get => currentGameSessionIconPath;
            private set => SetValue(ref currentGameSessionIconPath, value);
        }

        public string CurrentMediaSessionId
        {
            get => currentMediaSessionId;
            private set => SetValue(ref currentMediaSessionId, value);
        }

        public string CurrentMediaSessionName
        {
            get => currentMediaSessionName;
            private set => SetValue(ref currentMediaSessionName, value);
        }

        public string CurrentMediaSessionProcessName
        {
            get => currentMediaSessionProcessName;
            private set => SetValue(ref currentMediaSessionProcessName, value);
        }

        public string CurrentMediaSessionProcessPath
        {
            get => currentMediaSessionProcessPath;
            private set => SetValue(ref currentMediaSessionProcessPath, value);
        }

        public string CurrentMediaSessionIconPath
        {
            get => currentMediaSessionIconPath;
            private set => SetValue(ref currentMediaSessionIconPath, value);
        }

        public float CurrentMediaSessionVolume
        {
            get => currentMediaSessionVolume;
            set
            {
                var normalized = Math.Max(0f, Math.Min(1f, value));
                if (isRefreshingMediaSessionVolume)
                {
                    SetValue(ref currentMediaSessionVolume, normalized);
                    return;
                }

                QueueMediaSessionVolume(CurrentMediaSessionId, normalized);
            }
        }

        public int CurrentMediaSessionVolumePercent
        {
            get => currentMediaSessionVolumePercent;
            set
            {
                var normalized = Math.Max(0, Math.Min(100, value));
                if (isRefreshingMediaSessionVolume)
                {
                    SetValue(ref currentMediaSessionVolumePercent, normalized);
                    return;
                }

                QueueMediaSessionVolume(CurrentMediaSessionId, normalized / 100f);
            }
        }

        public string CurrentMediaSessionVolumeLabel
        {
            get => currentMediaSessionVolumeLabel;
            private set => SetValue(ref currentMediaSessionVolumeLabel, value);
        }

        public bool IsCurrentMediaSessionMuted
        {
            get => isCurrentMediaSessionMuted;
            private set => SetValue(ref isCurrentMediaSessionMuted, value);
        }

        public bool HasMediaSessions
        {
            get => hasMediaSessions;
            private set => SetValue(ref hasMediaSessions, value);
        }

        public bool HasSelectedMediaSession
        {
            get => hasSelectedMediaSession;
            private set => SetValue(ref hasSelectedMediaSession, value);
        }

        public Geometry CurrentMediaSessionVolumeIconGeometry
        {
            get => currentMediaSessionVolumeIconGeometry;
            private set => SetValue(ref currentMediaSessionVolumeIconGeometry, value);
        }

        public string LastChangeType
        {
            get => lastChangeType;
            private set => SetValue(ref lastChangeType, value);
        }

        public string LastChangeMessage
        {
            get => lastChangeMessage;
            private set => SetValue(ref lastChangeMessage, value);
        }

        public DateTime LastChangeAt
        {
            get => lastChangeAt;
            private set => SetValue(ref lastChangeAt, value);
        }

        public Geometry LastChangeIconGeometry
        {
            get => lastChangeIconGeometry;
            private set => SetValue(ref lastChangeIconGeometry, value);
        }

        public int VolumeStepPercent
        {
            get => volumeStepPercent;
            set
            {
                var normalized = Math.Max(1, Math.Min(50, value));
                if (volumeStepPercent == normalized)
                {
                    return;
                }

                SetValue(ref volumeStepPercent, normalized);
                plugin.SetVolumeStepPercent(normalized);
            }
        }

        public int HighlightedDeviceIndex
        {
            get => highlightedDeviceIndex;
            private set => SetValue(ref highlightedDeviceIndex, value);
        }

        public void ToggleSelector()
        {
            if (!IsSelectorOpen)
            {
                MarkSelectorOpened();
            }

            IsSelectorOpen = !IsSelectorOpen;
            Refresh();
            FocusSelectorIfOpen();
        }

        public void OpenSelector()
        {
            MarkSelectorOpened();
            IsSelectorOpen = true;
            Refresh();
            FocusSelectorIfOpen();
        }

        public void CloseSelector()
        {
            IsSelectorOpen = false;
            Refresh();
        }

        public void MoveHighlight(int direction)
        {
            if (Devices.Count == 0)
            {
                HighlightedDeviceIndex = -1;
                return;
            }

            var nextIndex = HighlightedDeviceIndex < 0 ? 0 : HighlightedDeviceIndex + direction;
            if (nextIndex < 0)
            {
                nextIndex = Devices.Count - 1;
            }
            else if (nextIndex >= Devices.Count)
            {
                nextIndex = 0;
            }

            SetHighlightedDeviceIndex(nextIndex);
        }

        public void SelectHighlightedDevice()
        {
            if (DateTime.UtcNow < confirmAvailableAt)
            {
                return;
            }

            if (HighlightedDeviceIndex < 0 || HighlightedDeviceIndex >= Devices.Count)
            {
                return;
            }

            SetDevice(Devices[HighlightedDeviceIndex].Id);
        }

        private void MarkSelectorOpened()
        {
            confirmAvailableAt = DateTime.UtcNow.AddMilliseconds(250);
        }

        private void FocusSelectorIfOpen()
        {
            if (IsSelectorOpen)
            {
                plugin.FocusThemeSelector();
            }
        }

        public void Refresh()
        {
            var currentOutput = plugin.GetCurrentPlaybackDeviceForTheme();
            var currentId = currentOutput?.Id;
            CurrentDeviceId = currentId;
            CurrentDeviceName = currentOutput != null ? plugin.GetDeviceDisplayNameForTheme(currentOutput) : plugin.Loc("LOCAS_Audio");
            CurrentDeviceLabel = currentOutput != null ? plugin.GetDeviceDisplayLabelForTheme(currentOutput) : plugin.Loc("LOCAS_Audio");
            CurrentDeviceIconGeometry = plugin.GetDeviceIconGeometryForTheme(currentOutput, false) ?? plugin.GetIconGeometry("volume-2");
            RefreshVolume();

            var currentInput = plugin.GetCurrentRecordingDeviceForTheme();
            CurrentInputDeviceId = currentInput?.Id;
            CurrentInputDeviceName = currentInput != null ? plugin.GetInputDeviceDisplayNameForTheme(currentInput) : plugin.Loc("LOCAS_AudioInput");
            CurrentInputDeviceLabel = currentInput != null ? plugin.GetInputDeviceDisplayLabelForTheme(currentInput) : plugin.Loc("LOCAS_AudioInput");
            CurrentInputDeviceIconGeometry = plugin.GetDeviceIconGeometryForTheme(currentInput, true) ?? plugin.GetIconGeometry("mic");
            RefreshInputVolume();
            CurrentGameName = plugin.GetCurrentGameName();
            RefreshGameVolume();
            RefreshGameSessionInfo();
            RefreshMediaSessions();

            var allOutputDevices = plugin.GetThemeSelectorDevices(true, currentId);
            var devices = allOutputDevices
                .Where(a => a.IsVisible)
                .OrderBy(a => a.EffectiveName)
                .Select(device => CreateThemeDevice(device, currentId))
                .ToList();
            var allDevices = allOutputDevices
                .OrderBy(a => a.EffectiveName)
                .Select(device => CreateThemeDevice(device, currentId))
                .ToList();

            var previousHighlightedId = HighlightedDeviceIndex >= 0 && HighlightedDeviceIndex < Devices.Count
                ? Devices[HighlightedDeviceIndex].Id
                : null;

            SynchronizeCollection(Devices, devices, a => a.Id, UpdateThemeDevice);
            SynchronizeCollection(AllDevices, allDevices, a => a.Id, UpdateThemeDevice);

            HasDevices = Devices.Count > 0;
            RestoreHighlightedDevice(previousHighlightedId, currentId);
            RefreshInputDevices(currentInputId: CurrentInputDeviceId);
        }

        public void RecordChange(string changeType, string message, Geometry iconGeometry = null)
        {
            LastChangeType = changeType ?? string.Empty;
            LastChangeMessage = message ?? string.Empty;
            LastChangeIconGeometry = iconGeometry;
            LastChangeAt = DateTime.Now;
        }

        private AudioSwitcherThemeDevice CreateThemeDevice(AudioDevice device, string currentId)
        {
            return new AudioSwitcherThemeDevice
            {
                Id = device.Id,
                Name = device.EffectiveName,
                WindowsName = device.Name,
                DisplayName = device.SettingsDisplayName,
                Icon = device.EffectiveIcon,
                IconGeometry = plugin.GetIconGeometry(string.IsNullOrWhiteSpace(device.EffectiveIcon) ? "volume-2" : device.EffectiveIcon),
                IsVisible = device.IsVisible,
                IsCurrent = string.Equals(device.Id, currentId, StringComparison.OrdinalIgnoreCase)
            };
        }

        private AudioSwitcherThemeDevice CreateThemeInputDevice(AudioDevice device, string currentId)
        {
            return new AudioSwitcherThemeDevice
            {
                Id = device.Id,
                Name = device.EffectiveName,
                WindowsName = device.Name,
                DisplayName = device.SettingsDisplayName,
                Icon = device.EffectiveIcon,
                IconGeometry = plugin.GetIconGeometry(string.IsNullOrWhiteSpace(device.EffectiveIcon) ? "mic" : device.EffectiveIcon),
                IsVisible = device.IsVisible,
                IsCurrent = string.Equals(device.Id, currentId, StringComparison.OrdinalIgnoreCase)
            };
        }

        private void RefreshInputDevices(string currentInputId)
        {
            var allInputDeviceSnapshots = plugin.GetThemeSelectorInputDevices(true, currentInputId);
            var inputDevices = allInputDeviceSnapshots
                .Where(a => a.IsVisible)
                .OrderBy(a => a.EffectiveName)
                .Select(device => CreateThemeInputDevice(device, currentInputId))
                .ToList();
            var allInputDevices = allInputDeviceSnapshots
                .OrderBy(a => a.EffectiveName)
                .Select(device => CreateThemeInputDevice(device, currentInputId))
                .ToList();

            SynchronizeCollection(InputDevices, inputDevices, a => a.Id, UpdateThemeDevice);
            SynchronizeCollection(AllInputDevices, allInputDevices, a => a.Id, UpdateThemeDevice);

            HasInputDevices = InputDevices.Count > 0;
        }

        internal void RefreshMediaSessions()
        {
            RefreshMediaSessions(plugin.GetMediaAudioSessions());
        }

        internal void RefreshMediaSessions(IReadOnlyList<AudioSessionInfo> sourceSessions)
        {
            sourceSessions = sourceSessions ?? new List<AudioSessionInfo>();
            var current = plugin.ResolveCurrentMediaSession(sourceSessions);
            var currentMediaSessionId = current?.Id;
            var sessions = sourceSessions
                .Select(session => CreateThemeMediaSession(session, currentMediaSessionId))
                .ToList();

            SynchronizeCollection(MediaSessions, sessions, a => a.Id, UpdateThemeMediaSession);

            HasMediaSessions = MediaSessions.Count > 0;

            CurrentMediaSessionId = current?.Id ?? string.Empty;
            CurrentMediaSessionName = current != null ? plugin.GetMediaSessionDisplayName(current) : plugin.Loc("LOCAS_MediaSessionUnavailable");
            CurrentMediaSessionProcessName = current?.ProcessName ?? string.Empty;
            CurrentMediaSessionProcessPath = current?.ProcessPath ?? string.Empty;
            CurrentMediaSessionIconPath = current?.IconPath ?? string.Empty;

            var state = current == null
                ? new AudioVolumeState { IsAvailable = false }
                : new AudioVolumeState { IsAvailable = true, Volume = current.Volume, IsMuted = current.IsMuted };
            SetCurrentMediaSessionVolumeState(state);
        }

        internal void RefreshMediaSessionState(string sessionId)
        {
            if (string.IsNullOrWhiteSpace(sessionId))
            {
                return;
            }

            var state = plugin.GetMediaSessionVolumeState(sessionId);
            var session = MediaSessions.FirstOrDefault(a => string.Equals(a.Id, sessionId, StringComparison.OrdinalIgnoreCase));
            if (session != null)
            {
                SetThemeMediaSessionVolumeState(session, state);
            }

            if (string.Equals(CurrentMediaSessionId, sessionId, StringComparison.OrdinalIgnoreCase))
            {
                SetCurrentMediaSessionVolumeState(state);
            }
        }

        private void SetCurrentMediaSessionVolumeState(AudioVolumeState state)
        {
            isRefreshingMediaSessionVolume = true;
            try
            {
                HasSelectedMediaSession = state.IsAvailable;
                CurrentMediaSessionVolume = state.Volume;
                CurrentMediaSessionVolumePercent = state.VolumePercent;
                IsCurrentMediaSessionMuted = state.IsMuted;
                CurrentMediaSessionVolumeLabel = state.IsAvailable
                    ? state.IsMuted ? plugin.Loc("LOCAS_MediaSessionMuted") : $"{state.VolumePercent}%"
                    : plugin.Loc("LOCAS_MediaSessionUnavailable");
                CurrentMediaSessionVolumeIconGeometry = GetVolumeIconGeometry(!state.IsAvailable || state.IsMuted, state.VolumePercent, false);
            }
            finally
            {
                isRefreshingMediaSessionVolume = false;
            }
        }

        private void SetThemeMediaSessionVolumeState(AudioSwitcherThemeMediaSession session, AudioVolumeState state)
        {
            if (session == null)
            {
                return;
            }

            var label = state.IsAvailable
                ? state.IsMuted ? plugin.Loc("LOCAS_MediaSessionMuted") : $"{state.VolumePercent}%"
                : plugin.Loc("LOCAS_MediaSessionUnavailable");
            session.UpdateVolumeState(state.Volume, state.IsMuted, label);
        }

        private AudioSwitcherThemeMediaSession CreateThemeMediaSession(AudioSessionInfo session, string currentId)
        {
            var isCurrent = string.Equals(session.Id, currentId, StringComparison.OrdinalIgnoreCase);
            var themeSession = new AudioSwitcherThemeMediaSession(
                QueueMediaSessionVolume,
                ChangeMediaSessionVolumeById,
                ToggleMediaSessionMuteById)
            {
                Id = session.Id,
                Name = plugin.GetMediaSessionDisplayName(session),
                ProcessName = session.ProcessName,
                ProcessPath = session.ProcessPath,
                IconPath = session.IconPath,
                AppIconPath = session.ProcessPath,
                ShowIcon = plugin.Settings.ShowMediaSessionIcons && !string.IsNullOrWhiteSpace(session.ProcessPath),
                DisplayName = isCurrent ? $"✓ {plugin.GetMediaSessionDisplayName(session)}" : plugin.GetMediaSessionDisplayName(session),
                IconGeometry = GetMediaSessionIconGeometry(session),
                IsCurrent = isCurrent
            };
            themeSession.UpdateVolumeState(
                session.Volume,
                session.IsMuted,
                session.IsMuted ? plugin.Loc("LOCAS_MediaSessionMuted") : $"{session.VolumePercent}%");
            return themeSession;
        }

        private static void SynchronizeCollection<T>(
            ObservableCollection<T> target,
            IReadOnlyList<T> desired,
            Func<T, string> getId,
            Action<T, T> update)
        {
            for (var desiredIndex = 0; desiredIndex < desired.Count; desiredIndex++)
            {
                var desiredItem = desired[desiredIndex];
                var desiredId = getId(desiredItem);
                var existingIndex = -1;
                for (var currentIndex = desiredIndex; currentIndex < target.Count; currentIndex++)
                {
                    if (string.Equals(getId(target[currentIndex]), desiredId, StringComparison.OrdinalIgnoreCase))
                    {
                        existingIndex = currentIndex;
                        break;
                    }
                }

                if (existingIndex < 0)
                {
                    target.Insert(desiredIndex, desiredItem);
                    continue;
                }

                var existingItem = target[existingIndex];
                update(existingItem, desiredItem);
                if (existingIndex != desiredIndex)
                {
                    target.Move(existingIndex, desiredIndex);
                }
            }

            while (target.Count > desired.Count)
            {
                target.RemoveAt(target.Count - 1);
            }
        }

        private static void UpdateThemeDevice(AudioSwitcherThemeDevice target, AudioSwitcherThemeDevice source)
        {
            target.Name = source.Name;
            target.WindowsName = source.WindowsName;
            target.DisplayName = source.DisplayName;
            target.Icon = source.Icon;
            target.IconGeometry = source.IconGeometry;
            target.IsVisible = source.IsVisible;
            target.IsCurrent = source.IsCurrent;
        }

        private static void UpdateThemeMediaSession(AudioSwitcherThemeMediaSession target, AudioSwitcherThemeMediaSession source)
        {
            target.Name = source.Name;
            target.ProcessName = source.ProcessName;
            target.ProcessPath = source.ProcessPath;
            target.IconPath = source.IconPath;
            target.AppIconPath = source.AppIconPath;
            target.ShowIcon = source.ShowIcon;
            target.DisplayName = source.DisplayName;
            target.UpdateVolumeState(source.Volume, source.IsMuted, source.VolumeLabel);
            target.IconGeometry = source.IconGeometry;
            target.IsCurrent = source.IsCurrent;
        }

        private Geometry GetMediaSessionIconGeometry(AudioSessionInfo session)
        {
            var processName = session?.ProcessName?.ToLowerInvariant() ?? string.Empty;
            if (processName.Contains("spotify") ||
                processName.Contains("music") ||
                processName.Contains("uniplaysong"))
            {
                return plugin.GetIconGeometry("audio-lines");
            }

            if (processName.Contains("chrome") ||
                processName.Contains("msedge") ||
                processName.Contains("firefox") ||
                processName.Contains("brave") ||
                processName.Contains("opera"))
            {
                return plugin.GetIconGeometry("monitor");
            }

            return plugin.GetIconGeometry("audio-waveform");
        }

        private void RestoreHighlightedDevice(string previousHighlightedId, string currentId)
        {
            var targetId = IsSelectorOpen ? previousHighlightedId : currentId;
            var index = Devices.ToList().FindIndex(device =>
                !string.IsNullOrWhiteSpace(targetId) &&
                string.Equals(device.Id, targetId, StringComparison.OrdinalIgnoreCase));

            if (index < 0)
            {
                index = Devices.ToList().FindIndex(device => device.IsCurrent);
            }

            if (index < 0 && Devices.Count > 0)
            {
                index = 0;
            }

            SetHighlightedDeviceIndex(index);
        }

        private void SetHighlightedDeviceIndex(int index)
        {
            HighlightedDeviceIndex = index;
            for (var i = 0; i < Devices.Count; i++)
            {
                Devices[i].IsHighlighted = i == index;
            }
        }

        private void SetDevice(object parameter)
        {
            var deviceId = parameter?.ToString();
            if (string.IsNullOrWhiteSpace(deviceId))
            {
                return;
            }

            plugin.SetThemeSelectedDevice(deviceId);
            IsSelectorOpen = false;
            Refresh();
        }

        private void SetInputDevice(object parameter)
        {
            var deviceId = parameter?.ToString();
            if (string.IsNullOrWhiteSpace(deviceId))
            {
                return;
            }

            plugin.SetThemeSelectedInputDevice(deviceId);
            Refresh();
        }

        private void QueueOutputVolume(float volume)
        {
            var normalized = Math.Max(0f, Math.Min(1f, volume));
            var percent = (int)Math.Round(normalized * 100);
            SetVolumeState(normalized, percent, IsMuted);
            outputVolumeWriter.Queue(normalized);
        }

        private void QueueInputVolume(float volume)
        {
            var normalized = Math.Max(0f, Math.Min(1f, volume));
            var percent = (int)Math.Round(normalized * 100);
            SetInputVolumeState(normalized, percent, IsInputMuted);
            inputVolumeWriter.Queue(normalized);
        }

        private void QueueGameVolume(float volume)
        {
            var normalized = Math.Max(0f, Math.Min(1f, volume));
            var percent = (int)Math.Round(normalized * 100);
            SetGameVolumeState(normalized, percent, IsGameMuted, HasActiveGameAudioSession);
            gameVolumeWriter.Queue(normalized);
        }

        internal void QueueMediaSessionVolume(string sessionId, float volume)
        {
            if (string.IsNullOrWhiteSpace(sessionId))
            {
                return;
            }

            var normalized = Math.Max(0f, Math.Min(1f, volume));
            var state = new AudioVolumeState
            {
                IsAvailable = true,
                Volume = normalized,
                IsMuted = false
            };
            var session = MediaSessions.FirstOrDefault(a => string.Equals(a.Id, sessionId, StringComparison.OrdinalIgnoreCase));
            if (session != null)
            {
                state.IsMuted = session.IsMuted;
                SetThemeMediaSessionVolumeState(session, state);
            }

            if (string.Equals(CurrentMediaSessionId, sessionId, StringComparison.OrdinalIgnoreCase))
            {
                state.IsMuted = IsCurrentMediaSessionMuted;
                SetCurrentMediaSessionVolumeState(state);
            }

            mediaVolumeWriter.Queue(sessionId, normalized);
        }

        private void SynchronizeOutputVolume()
        {
            RefreshVolume();
            RecordChange("volume", $"{plugin.Loc("LOCAS_VolumeTitle")}: {CurrentVolumePercent}%", CurrentOutputVolumeIconGeometry);
        }

        private void SynchronizeInputVolume()
        {
            RefreshInputVolume();
            RecordChange("input-volume", $"{plugin.Loc("LOCAS_AudioInput")}: {CurrentInputVolumePercent}%", CurrentInputVolumeIconGeometry);
        }

        private void SynchronizeGameVolume()
        {
            RefreshGameVolume();
            if (HasActiveGameAudioSession)
            {
                RecordChange("game-volume", $"{plugin.Loc("LOCAS_GameVolumeTitle")}: {CurrentGameVolumePercent}%", CurrentGameVolumeIconGeometry);
            }
        }

        internal void RefreshOutputVolume()
        {
            RefreshVolume();
        }

        internal void RefreshInputVolume()
        {
            RefreshInputVolumeState();
        }

        internal void RefreshGameVolumeState()
        {
            RefreshGameVolume();
        }

        private void RefreshVolume()
        {
            VolumeStepPercent = plugin.Settings.VolumeStepPercent;

            try
            {
                var state = plugin.GetCurrentVolumeState();
                SetVolumeState(state.Volume, state.VolumePercent, state.IsMuted);
            }
            catch
            {
                SetVolumeState(0, 0, false, string.Empty);
            }
        }

        private void SetVolumeState(float volume, int volumePercent, bool muted, string label = null)
        {
            isRefreshingVolume = true;
            try
            {
                CurrentVolume = volume;
                CurrentVolumePercent = volumePercent;
                IsMuted = muted;
                IsOutputMuted = muted;
                CurrentVolumeLabel = label ?? (muted ? plugin.Loc("LOCAS_Muted") : $"{volumePercent}%");
                CurrentOutputVolumeIconGeometry = GetVolumeIconGeometry(muted, volumePercent, false);
            }
            finally
            {
                isRefreshingVolume = false;
            }
        }

        private void RefreshInputVolumeState()
        {
            try
            {
                var state = plugin.GetCurrentInputVolumeState();
                SetInputVolumeState(state.Volume, state.VolumePercent, state.IsMuted);
            }
            catch
            {
                SetInputVolumeState(0, 0, false, string.Empty);
            }
        }

        private void SetInputVolumeState(float volume, int volumePercent, bool muted, string label = null)
        {
            isRefreshingInputVolume = true;
            try
            {
                CurrentInputVolume = volume;
                CurrentInputVolumePercent = volumePercent;
                IsInputMuted = muted;
                CurrentInputVolumeLabel = label ?? (muted ? plugin.Loc("LOCAS_InputMuted") : $"{volumePercent}%");
                CurrentInputVolumeIconGeometry = GetVolumeIconGeometry(muted, volumePercent, true);
            }
            finally
            {
                isRefreshingInputVolume = false;
            }
        }

        private void RefreshGameVolume()
        {
            try
            {
                var state = plugin.GetCurrentGameVolumeState();
                SetGameVolumeState(state.Volume, state.VolumePercent, state.IsMuted, state.IsAvailable);
            }
            catch
            {
                SetGameVolumeState(0, 0, false, false, string.Empty);
            }
        }

        private void RefreshGameSessionInfo()
        {
            try
            {
                var session = plugin.GetCurrentGameSessionInfo();
                CurrentGameProcessName = session?.ProcessName ?? string.Empty;
                CurrentGameProcessPath = session?.ProcessPath ?? string.Empty;
                CurrentGameSessionName = session?.FriendlyName ?? string.Empty;
                CurrentGameSessionIconPath = session?.IconPath ?? string.Empty;
                UpdateGameSessionStatusLabel();
            }
            catch
            {
                CurrentGameProcessName = string.Empty;
                CurrentGameProcessPath = string.Empty;
                CurrentGameSessionName = string.Empty;
                CurrentGameSessionIconPath = string.Empty;
                UpdateGameSessionStatusLabel();
            }
        }

        private void SetGameVolumeState(float volume, int volumePercent, bool muted, bool isAvailable, string label = null)
        {
            isRefreshingGameVolume = true;
            try
            {
                CurrentGameVolume = volume;
                CurrentGameVolumePercent = volumePercent;
                IsGameMuted = muted;
                HasActiveGameAudioSession = isAvailable;
                CurrentGameVolumeLabel = label ?? (isAvailable
                    ? muted ? plugin.Loc("LOCAS_GameMuted") : $"{volumePercent}%"
                    : plugin.Loc("LOCAS_GameVolumeUnavailable"));
                CurrentGameVolumeIconGeometry = GetVolumeIconGeometry(muted || !isAvailable, volumePercent, false);
                UpdateGameSessionStatusLabel();
            }
            finally
            {
                isRefreshingGameVolume = false;
            }
        }

        private Geometry GetVolumeIconGeometry(bool muted, int volumePercent, bool input)
        {
            if (muted)
            {
                return plugin.GetIconGeometry(input ? "mic-off" : "volume-x");
            }

            if (input)
            {
                return plugin.GetIconGeometry("mic");
            }

            if (volumePercent <= 0)
            {
                return plugin.GetIconGeometry("volume-off");
            }

            if (volumePercent < 50)
            {
                return plugin.GetIconGeometry("volume-1");
            }

            return plugin.GetIconGeometry("volume-2");
        }

        private void UpdateGameSessionStatusLabel()
        {
            if (string.IsNullOrWhiteSpace(CurrentGameName))
            {
                GameSessionStatusLabel = plugin.Loc("LOCAS_GameSessionNoGame");
                return;
            }

            if (!HasActiveGameAudioSession)
            {
                GameSessionStatusLabel = plugin.Loc("LOCAS_GameSessionWaiting");
                return;
            }

            var sessionName = !string.IsNullOrWhiteSpace(CurrentGameSessionName)
                ? CurrentGameSessionName
                : CurrentGameName;
            GameSessionStatusLabel = string.Format(plugin.Loc("LOCAS_GameSessionControlling"), sessionName);
        }

        private void ChangeVolume(int direction)
        {
            plugin.ChangeVolumeByStep(direction);
        }

        private void ChangeInputVolume(int direction)
        {
            plugin.ChangeInputVolumeByStep(direction);
        }

        private void ChangeGameVolume(int direction)
        {
            plugin.ChangeGameVolumeByStep(direction);
        }

        private void ToggleMute()
        {
            plugin.ToggleMute();
        }

        private void ToggleInputMute()
        {
            plugin.ToggleInputMute();
        }

        private void ToggleGameMute()
        {
            plugin.ToggleGameMute();
        }

        private void SetMediaSession(object parameter)
        {
            var sessionId = parameter?.ToString();
            if (string.IsNullOrWhiteSpace(sessionId))
            {
                return;
            }

            plugin.SetThemeSelectedMediaSession(sessionId);
        }

        private void ChangeMediaSessionVolume(object parameter, int direction)
        {
            var sessionId = parameter?.ToString();
            if (string.IsNullOrWhiteSpace(sessionId))
            {
                plugin.ChangeMediaSessionVolumeByStep(direction);
                return;
            }

            ChangeMediaSessionVolumeById(sessionId, direction);
        }

        private void ChangeMediaSessionVolumeById(string sessionId, int direction)
        {
            var session = MediaSessions.FirstOrDefault(a => string.Equals(a.Id, sessionId, StringComparison.OrdinalIgnoreCase));
            if (session == null)
            {
                return;
            }

            var step = Math.Max(1, plugin.Settings.VolumeStepPercent) / 100f;
            QueueMediaSessionVolume(sessionId, session.Volume + step * Math.Sign(direction));
        }

        private void ToggleMediaSessionMute(object parameter)
        {
            var sessionId = parameter?.ToString();
            if (string.IsNullOrWhiteSpace(sessionId))
            {
                plugin.ToggleMediaSessionMute();
                return;
            }

            ToggleMediaSessionMuteById(sessionId);
        }

        private void ToggleMediaSessionMuteById(string sessionId)
        {
            plugin.ToggleMediaSessionMute(sessionId);
        }

        private void SetVolume(object parameter)
        {
            if (!TryGetVolumeScalar(parameter, out var volume))
            {
                return;
            }

            CurrentVolume = volume;
        }

        private void SetInputVolume(object parameter)
        {
            if (!TryGetVolumeScalar(parameter, out var volume))
            {
                return;
            }

            CurrentInputVolume = volume;
        }

        private void SetGameVolume(object parameter)
        {
            if (!TryGetVolumeScalar(parameter, out var volume))
            {
                return;
            }

            CurrentGameVolume = volume;
        }

        private void SetMediaSessionVolume(object parameter)
        {
            if (!TryGetVolumeScalar(parameter, out var volume))
            {
                return;
            }

            QueueMediaSessionVolume(CurrentMediaSessionId, volume);
        }

        private static bool TryGetVolumeScalar(object parameter, out float volume)
        {
            volume = 0;
            if (parameter == null)
            {
                return false;
            }

            switch (parameter)
            {
                case float floatValue:
                    volume = NormalizeVolumeValue(floatValue);
                    return true;
                case double doubleValue:
                    volume = NormalizeVolumeValue((float)doubleValue);
                    return true;
                case int intValue:
                    volume = NormalizeVolumeValue(intValue);
                    return true;
                case string text:
                    var value = text.Trim();
                    if (value.EndsWith("%", StringComparison.Ordinal))
                    {
                        value = value.Substring(0, value.Length - 1);
                    }

                    if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var invariantValue) ||
                        float.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out invariantValue))
                    {
                        volume = NormalizeVolumeValue(invariantValue);
                        return true;
                    }

                    return false;
                default:
                    return false;
            }
        }

        private static float NormalizeVolumeValue(float value)
        {
            var scalar = value > 1 ? value / 100f : value;
            return Math.Max(0f, Math.Min(1f, scalar));
        }

    }
}
