using System.Collections.Generic;
using System.Windows.Media;
using Playnite.SDK;
using Playnite.SDK.Data;

namespace PlayniteAudioSwitcher
{
    public sealed class AudioSwitcherThemeDevice : ObservableObject
    {
        private bool isCurrent;
        private bool isHighlighted;

        public string Id { get; set; }

        public string Name { get; set; }

        public string WindowsName { get; set; }

        public string DisplayName { get; set; }

        public string Icon { get; set; }

        public Geometry IconGeometry { get; set; }

        public bool IsCurrent
        {
            get => isCurrent;
            set => SetValue(ref isCurrent, value);
        }

        public bool IsHighlighted
        {
            get => isHighlighted;
            set => SetValue(ref isHighlighted, value);
        }

        public string CurrentMarker => IsCurrent ? "\u2713" : string.Empty;
    }
}
