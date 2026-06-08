using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace PlayniteAudioSwitcher
{
    public sealed class IconGeometryConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var data = value as string;
            if (string.IsNullOrWhiteSpace(data))
            {
                return Geometry.Empty;
            }

            return Geometry.Parse(data);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }
}
