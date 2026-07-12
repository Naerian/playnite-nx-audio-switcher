using System.Collections.Generic;

namespace PlayniteAudioSwitcher
{
    public sealed class AudioSessionInfo
    {
        public string Id { get; set; }

        public uint ProcessId { get; set; }

        public string ProcessName { get; set; }

        public string ProcessPath { get; set; }

        public string DisplayName { get; set; }

        public string IconPath { get; set; }

        public string SessionIdentifier { get; set; }

        public List<string> SourceSessionIds { get; set; } = new List<string>();

        public int State { get; set; }

        public bool IsActive => State == 1;

        public int VolumePercent { get; set; }

        public float Volume { get; set; }

        public bool IsMuted { get; set; }

        public string FriendlyName
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(DisplayName))
                {
                    return DisplayName;
                }

                if (!string.IsNullOrWhiteSpace(ProcessName))
                {
                    return ProcessName;
                }

                return ProcessId > 0 ? $"PID {ProcessId}" : string.Empty;
            }
        }
    }
}
