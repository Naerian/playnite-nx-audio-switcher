using System;

namespace PlayniteAudioSwitcher
{
    public sealed class GameAudioProfileEntry
    {
        public Guid GameId { get; set; }

        public string GameName { get; set; }

        public string GameImagePath { get; set; }

        public string DeviceId { get; set; }

        public string InputDeviceId { get; set; }

        public string SpatialSoundMode { get; set; }

        public int? GameVolumePercent { get; set; }

        public bool IsEmpty => string.IsNullOrWhiteSpace(DeviceId) &&
            string.IsNullOrWhiteSpace(InputDeviceId) &&
            string.IsNullOrWhiteSpace(SpatialSoundMode) &&
            !GameVolumePercent.HasValue;

        public GameAudioProfileEntry Clone()
        {
            return new GameAudioProfileEntry
            {
                GameId = GameId,
                GameName = GameName,
                GameImagePath = GameImagePath,
                DeviceId = DeviceId,
                InputDeviceId = InputDeviceId,
                SpatialSoundMode = SpatialSoundMode,
                GameVolumePercent = GameVolumePercent
            };
        }

        public GameAudioProfile ToProfile()
        {
            return new GameAudioProfile
            {
                DeviceId = DeviceId,
                InputDeviceId = InputDeviceId,
                SpatialSoundMode = SpatialSoundMode,
                GameVolumePercent = GameVolumePercent
            };
        }
    }
}
