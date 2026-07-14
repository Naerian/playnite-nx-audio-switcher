using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Input;
using System.Windows.Media;
using Playnite.SDK;
using Playnite.SDK.Data;

namespace PlayniteAudioSwitcher
{
    public sealed class AudioSwitcherThemeMediaSession : ObservableObject
    {
        private readonly Action<string, float> setVolume;
        private readonly Action<string, int> changeVolume;
        private readonly Action<string> toggleMute;
        private string id;
        private string name;
        private string processName;
        private string processPath;
        private string iconPath;
        private string appIconPath;
        private bool showIcon;
        private string displayName;
        private int volumePercent;
        private float volume;
        private string volumeLabel;
        private bool isMuted;
        private Geometry iconGeometry;
        private bool isCurrent;

        public AudioSwitcherThemeMediaSession()
            : this(null, null, null)
        {
        }

        internal AudioSwitcherThemeMediaSession(
            Action<string, float> setVolume,
            Action<string, int> changeVolume,
            Action<string> toggleMute)
        {
            this.setVolume = setVolume;
            this.changeVolume = changeVolume;
            this.toggleMute = toggleMute;
            SetVolumeCommand = new RelayCommand<object>(SetVolumeFromCommand);
            VolumeUpCommand = new RelayCommand(() => ChangeVolume(1));
            VolumeDownCommand = new RelayCommand(() => ChangeVolume(-1));
            ToggleMuteCommand = new RelayCommand(ToggleMute);
        }

        public ICommand SetVolumeCommand { get; }

        public ICommand VolumeUpCommand { get; }

        public ICommand VolumeDownCommand { get; }

        public ICommand ToggleMuteCommand { get; }

        public string Id
        {
            get => id;
            set => SetValue(ref id, value);
        }

        public string Name
        {
            get => name;
            set => SetValue(ref name, value);
        }

        public string ProcessName
        {
            get => processName;
            set => SetValue(ref processName, value);
        }

        public string ProcessPath
        {
            get => processPath;
            set => SetValue(ref processPath, value);
        }

        public string IconPath
        {
            get => iconPath;
            set => SetValue(ref iconPath, value);
        }

        public string AppIconPath
        {
            get => appIconPath;
            set => SetValue(ref appIconPath, value);
        }

        public bool ShowIcon
        {
            get => showIcon;
            set => SetValue(ref showIcon, value);
        }

        public string DisplayName
        {
            get => displayName;
            set => SetValue(ref displayName, value);
        }

        public int VolumePercent
        {
            get => volumePercent;
            set
            {
                var normalizedPercent = Math.Max(0, Math.Min(100, value));
                if (volumePercent == normalizedPercent)
                {
                    return;
                }

                SetWritableVolume(normalizedPercent / 100f);
            }
        }

        public float Volume
        {
            get => volume;
            set
            {
                var normalized = Math.Max(0f, Math.Min(1f, value));
                if (Math.Abs(volume - normalized) < 0.0005f)
                {
                    return;
                }

                SetWritableVolume(normalized);
            }
        }

        public string VolumeLabel
        {
            get => volumeLabel;
            set => SetValue(ref volumeLabel, value);
        }

        public bool IsMuted
        {
            get => isMuted;
            set => SetValue(ref isMuted, value);
        }

        public Geometry IconGeometry
        {
            get => iconGeometry;
            set => SetValue(ref iconGeometry, value);
        }

        public bool IsCurrent
        {
            get => isCurrent;
            set
            {
                if (isCurrent != value)
                {
                    SetValue(ref isCurrent, value);
                    OnPropertyChanged(nameof(CurrentMarker));
                }
            }
        }

        public string CurrentMarker => IsCurrent ? "\u2713" : string.Empty;

        internal void UpdateVolumeState(float value, bool muted, string label)
        {
            var normalized = Math.Max(0f, Math.Min(1f, value));
            SetValue(ref volume, normalized, nameof(Volume));
            SetValue(ref volumePercent, (int)Math.Round(normalized * 100), nameof(VolumePercent));
            SetValue(ref isMuted, muted, nameof(IsMuted));
            SetValue(ref volumeLabel, label, nameof(VolumeLabel));
        }

        private void SetWritableVolume(float value)
        {
            var normalized = Math.Max(0f, Math.Min(1f, value));
            var percent = (int)Math.Round(normalized * 100);
            SetValue(ref volume, normalized, nameof(Volume));
            SetValue(ref volumePercent, percent, nameof(VolumePercent));
            if (!IsMuted)
            {
                SetValue(ref volumeLabel, $"{percent}%", nameof(VolumeLabel));
            }

            if (!string.IsNullOrWhiteSpace(Id))
            {
                setVolume?.Invoke(Id, normalized);
            }
        }

        private void SetVolumeFromCommand(object parameter)
        {
            if (TryGetVolumeScalar(parameter, out var scalar))
            {
                Volume = scalar;
            }
        }

        private void ChangeVolume(int direction)
        {
            if (!string.IsNullOrWhiteSpace(Id))
            {
                changeVolume?.Invoke(Id, Math.Sign(direction));
            }
        }

        private void ToggleMute()
        {
            if (!string.IsNullOrWhiteSpace(Id))
            {
                toggleMute?.Invoke(Id);
            }
        }

        private static bool TryGetVolumeScalar(object parameter, out float volumeScalar)
        {
            volumeScalar = 0;
            if (parameter == null)
            {
                return false;
            }

            try
            {
                var value = Convert.ToDouble(parameter, CultureInfo.InvariantCulture);
                if (double.IsNaN(value) || double.IsInfinity(value))
                {
                    return false;
                }

                volumeScalar = (float)Math.Max(0d, Math.Min(1d, value > 1d ? value / 100d : value));
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
