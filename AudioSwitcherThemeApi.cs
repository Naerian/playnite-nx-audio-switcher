using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
        private string preferredDeviceName;
        private bool hasDevices;
        private int highlightedDeviceIndex = -1;
        private DateTime confirmAvailableAt = DateTime.MinValue;

        public AudioSwitcherThemeApi(AudioSwitcherPlugin plugin)
        {
            this.plugin = plugin;
            Devices = new ObservableCollection<AudioSwitcherThemeDevice>();
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
            SetPreferredDeviceCommand = new RelayCommand<object>(SetPreferredDevice);
        }

        public ObservableCollection<AudioSwitcherThemeDevice> Devices { get; }

        public ICommand ToggleSelectorCommand { get; }

        public ICommand OpenSelectorCommand { get; }

        public ICommand CloseSelectorCommand { get; }

        public ICommand NextDeviceCommand { get; }

        public ICommand RefreshDevicesCommand { get; }

        public ICommand SetDeviceCommand { get; }

        public ICommand SetPreferredDeviceCommand { get; }

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

        public string PreferredDeviceName
        {
            get => preferredDeviceName;
            private set => SetValue(ref preferredDeviceName, value);
        }

        public bool HasDevices
        {
            get => hasDevices;
            private set => SetValue(ref hasDevices, value);
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
        }

        public void OpenSelector()
        {
            MarkSelectorOpened();
            IsSelectorOpen = true;
            Refresh();
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

        public void Refresh()
        {
            var currentId = plugin.GetCurrentDeviceId();
            var preferredId = plugin.Settings.FullscreenPreferredDeviceId;
            CurrentDeviceId = currentId;
            CurrentDeviceName = plugin.GetCurrentDeviceDisplayName();
            CurrentDeviceLabel = plugin.GetCurrentDeviceDisplayLabel();
            CurrentDeviceIconGeometry = plugin.GetCurrentDeviceIconGeometry() ?? plugin.GetIconGeometry("volume-2");
            PreferredDeviceName = string.IsNullOrWhiteSpace(preferredId) ? string.Empty : plugin.GetDeviceDisplayNameForTheme(preferredId);

            var devices = plugin.GetThemeSelectorDevices()
                .OrderBy(a => a.EffectiveName)
                .Select(device => new AudioSwitcherThemeDevice
                {
                    Id = device.Id,
                    Name = device.EffectiveName,
                    WindowsName = device.Name,
                    DisplayName = device.SettingsDisplayName,
                    Icon = device.EffectiveIcon,
                    IconGeometry = plugin.GetIconGeometry(string.IsNullOrWhiteSpace(device.EffectiveIcon) ? "volume-2" : device.EffectiveIcon),
                    IsCurrent = string.Equals(device.Id, currentId, StringComparison.OrdinalIgnoreCase),
                    IsPreferred = string.Equals(device.Id, preferredId, StringComparison.OrdinalIgnoreCase)
                })
                .ToList();

            var previousHighlightedId = HighlightedDeviceIndex >= 0 && HighlightedDeviceIndex < Devices.Count
                ? Devices[HighlightedDeviceIndex].Id
                : null;

            Devices.Clear();
            foreach (var device in devices)
            {
                Devices.Add(device);
            }

            HasDevices = Devices.Count > 0;
            RestoreHighlightedDevice(previousHighlightedId, currentId);
        }

        private void RestoreHighlightedDevice(string previousHighlightedId, string currentId)
        {
            var preferredId = IsSelectorOpen ? previousHighlightedId : currentId;
            var index = Devices.ToList().FindIndex(device =>
                !string.IsNullOrWhiteSpace(preferredId) &&
                string.Equals(device.Id, preferredId, StringComparison.OrdinalIgnoreCase));

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

        private void SetPreferredDevice(object parameter)
        {
            var deviceId = parameter?.ToString();
            if (string.IsNullOrWhiteSpace(deviceId))
            {
                return;
            }

            plugin.SetThemePreferredDevice(deviceId);
            Refresh();
        }
    }
}
