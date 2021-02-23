using LiveSense.Service;
using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace LiveSense.Common.Converters
{
    public class ServiceStatusToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value switch
            {
                ServiceStatus.Connected => new SolidColorBrush(Color.FromRgb(0x00, 0x80, 0x00)),
                ServiceStatus.Disconnected => new SolidColorBrush(Color.FromRgb(0xf5, 0x3e, 0x2e)),
                ServiceStatus.Connecting or ServiceStatus.Disconnecting => new SolidColorBrush(Color.FromRgb(0xb3, 0x9c, 0x09)),
                _ => null
            };

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
