using System;
using System.Collections.Generic;

namespace PlayniteAudioSwitcher
{
    public sealed class AudioSwitcherBackup
    {
        public string Format { get; set; } = "AudioSwitcherBackup";

        public int FormatVersion { get; set; } = 1;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public AudioSwitcherSettings Settings { get; set; }

        public Dictionary<Guid, GameAudioProfile> GameProfiles { get; set; } = new Dictionary<Guid, GameAudioProfile>();
    }
}
