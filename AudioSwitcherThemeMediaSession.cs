using System.Windows.Media;

namespace PlayniteAudioSwitcher
{
    public sealed class AudioSwitcherThemeMediaSession
    {
        public string Id { get; set; }

        public string Name { get; set; }

        public string ProcessName { get; set; }

        public string ProcessPath { get; set; }

        public string IconPath { get; set; }

        public string AppIconPath { get; set; }

        public bool ShowIcon { get; set; }

        public string DisplayName { get; set; }

        public int VolumePercent { get; set; }

        public float Volume { get; set; }

        public string VolumeLabel { get; set; }

        public bool IsMuted { get; set; }

        public Geometry IconGeometry { get; set; }

        public bool IsCurrent { get; set; }

        public string CurrentMarker => IsCurrent ? "✓" : string.Empty;
    }
}
