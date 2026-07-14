using System;
using System.Windows.Input;

namespace PlayniteAudioSwitcher
{
    internal sealed class VolumeSliderAcceleration
    {
        private Key lastKey = Key.None;
        private DateTime lastInputAt = DateTime.MinValue;
        private int repeatCount;

        public int GetStep(Key key, bool isRepeat, int baseStep)
        {
            var now = DateTime.UtcNow;
            var isContinuousInput = key == lastKey &&
                (isRepeat || now - lastInputAt <= TimeSpan.FromMilliseconds(250));

            repeatCount = isContinuousInput ? repeatCount + 1 : 0;
            lastKey = key;
            lastInputAt = now;

            var multiplier = repeatCount >= 8 ? 4 : repeatCount >= 3 ? 2 : 1;
            return Math.Max(1, baseStep) * multiplier;
        }
    }
}
