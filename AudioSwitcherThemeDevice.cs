using System.Collections.Generic;
using System.Windows.Media;
using Playnite.SDK;
using Playnite.SDK.Data;

namespace PlayniteAudioSwitcher
{
    public sealed class AudioSwitcherThemeDevice : ObservableObject
    {
        private string id;
        private string name;
        private string windowsName;
        private string displayName;
        private string icon;
        private Geometry iconGeometry;
        private bool isVisible = true;
        private bool isCurrent;
        private bool isHighlighted;

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

        public string WindowsName
        {
            get => windowsName;
            set => SetValue(ref windowsName, value);
        }

        public string DisplayName
        {
            get => displayName;
            set => SetValue(ref displayName, value);
        }

        public string Icon
        {
            get => icon;
            set => SetValue(ref icon, value);
        }

        public Geometry IconGeometry
        {
            get => iconGeometry;
            set => SetValue(ref iconGeometry, value);
        }

        public bool IsVisible
        {
            get => isVisible;
            set => SetValue(ref isVisible, value);
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

        public bool IsHighlighted
        {
            get => isHighlighted;
            set => SetValue(ref isHighlighted, value);
        }

        public string CurrentMarker => IsCurrent ? "\u2713" : string.Empty;
    }
}
