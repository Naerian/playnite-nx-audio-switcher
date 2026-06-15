using System;

namespace PlayniteAudioSwitcher
{
    public sealed class AudioVolumeState
    {
        public float Volume { get; set; }

        public int VolumePercent => (int)Math.Round(Volume * 100);

        public bool IsMuted { get; set; }
    }
}
