namespace PlayniteAudioSwitcher
{
    public sealed class AudioDeviceAlias
    {
        public string DeviceId { get; set; }

        public string CustomName { get; set; }

        public string Icon { get; set; }

        public bool? IsVisible { get; set; }

        public int? DefaultVolumePercent { get; set; }
    }
}
