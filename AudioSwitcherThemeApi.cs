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
        private float currentVolume;
        private int currentVolumePercent;
        private string currentVolumeLabel;
        private bool isMuted;
        private int volumeStepPercent;
        private bool isRefreshingVolume;
        private int highlightedDeviceIndex = -1;
        private DateTime confirmAvailableAt = DateTime.MinValue;

        public AudioSwitcherThemeApi(AudioSwitcherPlugin plugin)
        {
            this.plugin = plugin;
            Devices = new ObservableCollection<AudioSwitcherThemeDevice>();
            AllDevices = new ObservableCollection<AudioSwitcherThemeDevice>();
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
        }

        public ObservableCollection<AudioSwitcherThemeDevice> Devices { get; }

        public ObservableCollection<AudioSwitcherThemeDevice> AllDevices { get; }

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

        public int VolumeStepPercent
        {
            get => volumeStepPercent;
            private set => SetValue(ref volumeStepPercent, value);
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
                CurrentVolumeLabel = label ?? (muted ? plugin.Loc("LOCAS_Muted") : $"{volumePercent}%");
            }
            finally
            {
                isRefreshingVolume = false;
            }
        }

        private void ChangeVolume(int direction)
        {
            plugin.ChangeVolumeByStep(direction);
            RefreshVolume();
        }

        private void ToggleMute()
        {
            plugin.ToggleMute();
            RefreshVolume();
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
