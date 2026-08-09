namespace PlayniteAudioSwitcher
{
    public sealed class AudioDeviceBatteryInfo
    {
        public int Percent { get; set; }

        public bool IsCharging { get; set; }

        public string Source { get; set; }
    }
}
