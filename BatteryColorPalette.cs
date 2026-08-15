using System.Windows.Media;

namespace PlayniteAudioSwitcher
{
    internal static class BatteryColorPalette
    {
        private static readonly Brush EmptyBrush = CreateBrush(224, 82, 82);
        private static readonly Brush LowBrush = CreateBrush(242, 153, 74);
        private static readonly Brush MediumBrush = CreateBrush(242, 201, 76);
        private static readonly Brush FullBrush = CreateBrush(79, 194, 126);
        private static readonly Brush UnavailableBrush = CreateBrush(138, 143, 152);

        public static Brush GetBrush(AudioDevice device)
        {
            return GetBrush(device?.BatteryPercent, device?.IsBatteryCharging == true);
        }

        public static Brush GetBrush(int? batteryPercent, bool isCharging)
        {
            if (!batteryPercent.HasValue)
            {
                return UnavailableBrush;
            }

            if (isCharging)
            {
                return FullBrush;
            }

            var percent = batteryPercent.Value;
            if (percent <= 0)
            {
                return EmptyBrush;
            }

            if (percent <= 20)
            {
                return LowBrush;
            }

            return percent <= 50 ? MediumBrush : FullBrush;
        }

        private static Brush CreateBrush(byte red, byte green, byte blue)
        {
            var brush = new SolidColorBrush(Color.FromRgb(red, green, blue));
            brush.Freeze();
            return brush;
        }
    }
}
