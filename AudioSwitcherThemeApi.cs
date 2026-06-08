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

        public void ToggleSelector()
        {
            IsSelectorOpen = !IsSelectorOpen;
            Refresh();
        }

        public void OpenSelector()
        {
            IsSelectorOpen = true;
            Refresh();
        }

        public void CloseSelector()
        {
            IsSelectorOpen = false;
            Refresh();
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

            Devices.Clear();
            foreach (var device in devices)
            {
                Devices.Add(device);
            }

            HasDevices = Devices.Count > 0;
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
