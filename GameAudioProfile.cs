namespace PlayniteAudioSwitcher
{
    public sealed class GameAudioProfile
    {
        public string DeviceId { get; set; }

        public string InputDeviceId { get; set; }

        public string SpatialSoundMode { get; set; }

        public int? GameVolumePercent { get; set; }
    }
}
