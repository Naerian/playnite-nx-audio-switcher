using System;

namespace PlayniteAudioSwitcher
{
    public sealed class AudioVolumeState
    {
        public bool IsAvailable { get; set; } = true;

        public float Volume { get; set; }

        public int VolumePercent => (int)Math.Round(Volume * 100);

        public bool IsMuted { get; set; }
    }
}
