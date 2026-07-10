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
        private string lastChangeType;
        private string lastChangeMessage;
        private DateTime lastChangeAt;
        private Geometry lastChangeIconGeometry;
        private int volumeStepPercent;
        private bool isRefreshingVolume;
        private bool isRefreshingInputVolume;
        private bool isRefreshingGameVolume;
        private int highlightedDeviceIndex = -1;
        private DateTime confirmAvailableAt = DateTime.MinValue;

        public AudioSwitcherThemeApi(AudioSwitcherPlugin plugin)
        {
            this.plugin = plugin;
            Devices = new ObservableCollection<AudioSwitcherThemeDevice>();
            AllDevices = new ObservableCollection<AudioSwitcherThemeDevice>();
            InputDevices = new ObservableCollection<AudioSwitcherThemeDevice>();
            AllInputDevices = new ObservableCollection<AudioSwitcherThemeDevice>();
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
        }

        public ObservableCollection<AudioSwitcherThemeDevice> Devices { get; }

        public ObservableCollection<AudioSwitcherThemeDevice> AllDevices { get; }

        public ObservableCollection<AudioSwitcherThemeDevice> InputDevices { get; }

        public ObservableCollection<AudioSwitcherThemeDevice> AllInputDevices { get; }

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

                plugin.SetVolume(normalized, false);
                RefreshVolume();
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

                plugin.SetVolume(normalized / 100f, false);
                RefreshVolume();
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

                plugin.SetInputVolume(normalized, false);
                RefreshInputVolume();
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

                plugin.SetInputVolume(normalized / 100f, false);
                RefreshInputVolume();
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

                plugin.SetGameVolume(normalized, false);
                RefreshGameVolume();
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

                plugin.SetGameVolume(normalized / 100f, false);
                RefreshGameVolume();
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
            var currentId = plugin.GetCurrentDeviceId();
            CurrentDeviceId = currentId;
            CurrentDeviceName = plugin.GetCurrentDeviceDisplayName();
            CurrentDeviceLabel = plugin.GetCurrentDeviceDisplayLabel();
            CurrentDeviceIconGeometry = plugin.GetCurrentDeviceIconGeometry() ?? plugin.GetIconGeometry("volume-2");
            RefreshVolume();
            CurrentInputDeviceId = plugin.GetCurrentInputDeviceId();
            CurrentInputDeviceName = plugin.GetCurrentInputDeviceDisplayName();
            CurrentInputDeviceLabel = plugin.GetCurrentInputDeviceDisplayLabel();
            CurrentInputDeviceIconGeometry = plugin.GetCurrentInputDeviceIconGeometry() ?? plugin.GetIconGeometry("mic");
            RefreshInputVolume();
            CurrentGameName = plugin.GetCurrentGameName();
            RefreshGameVolume();
            RefreshGameSessionInfo();

            var devices = plugin.GetThemeSelectorDevices(false)
                .OrderBy(a => a.EffectiveName)
                .Select(device => CreateThemeDevice(device, currentId))
                .ToList();
            var allDevices = plugin.GetThemeSelectorDevices(true)
                .OrderBy(a => a.EffectiveName)
                .Select(device => CreateThemeDevice(device, currentId))
                .ToList();

            var previousHighlightedId = HighlightedDeviceIndex >= 0 && HighlightedDeviceIndex < Devices.Count
                ? Devices[HighlightedDeviceIndex].Id
                : null;

            Devices.Clear();
            foreach (var device in devices)
            {
                Devices.Add(device);
            }

            AllDevices.Clear();
            foreach (var device in allDevices)
            {
                AllDevices.Add(device);
            }

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
            var inputDevices = plugin.GetThemeSelectorInputDevices(false)
                .OrderBy(a => a.EffectiveName)
                .Select(device => CreateThemeInputDevice(device, currentInputId))
                .ToList();
            var allInputDevices = plugin.GetThemeSelectorInputDevices(true)
                .OrderBy(a => a.EffectiveName)
                .Select(device => CreateThemeInputDevice(device, currentInputId))
                .ToList();

            InputDevices.Clear();
            foreach (var device in inputDevices)
            {
                InputDevices.Add(device);
            }

            AllInputDevices.Clear();
            foreach (var device in allInputDevices)
            {
                AllInputDevices.Add(device);
            }

            HasInputDevices = InputDevices.Count > 0;
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

        private void RefreshInputVolume()
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
            RefreshVolume();
        }

        private void ChangeInputVolume(int direction)
        {
            plugin.ChangeInputVolumeByStep(direction);
            RefreshInputVolume();
        }

        private void ChangeGameVolume(int direction)
        {
            plugin.ChangeGameVolumeByStep(direction);
            RefreshGameVolume();
        }

        private void ToggleMute()
        {
            plugin.ToggleMute();
            RefreshVolume();
        }

        private void ToggleInputMute()
        {
            plugin.ToggleInputMute();
            RefreshInputVolume();
        }

        private void ToggleGameMute()
        {
            plugin.ToggleGameMute();
            RefreshGameVolume();
        }

        private void SetVolume(object parameter)
        {
            if (!TryGetVolumeScalar(parameter, out var volume))
            {
                return;
            }

            plugin.SetVolume(volume, false);
            RefreshVolume();
        }

        private void SetInputVolume(object parameter)
        {
            if (!TryGetVolumeScalar(parameter, out var volume))
            {
                return;
            }

            plugin.SetInputVolume(volume, false);
            RefreshInputVolume();
        }

        private void SetGameVolume(object parameter)
        {
            if (!TryGetVolumeScalar(parameter, out var volume))
            {
                return;
            }

            plugin.SetGameVolume(volume, false);
            RefreshGameVolume();
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
