using System.Collections.Generic;
using System.Windows.Media;
using Playnite.SDK;
using Playnite.SDK.Data;

namespace PlayniteAudioSwitcher
{
    public sealed class AudioSwitcherThemeMediaSession : ObservableObject
    {
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
            set => SetValue(ref volumePercent, value);
        }

        public float Volume
        {
            get => volume;
            set => SetValue(ref volume, value);
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

        public string CurrentMarker => IsCurrent ? "✓" : string.Empty;
    }
}
