using System;
using System.Globalization;
using System.Windows.Data;

namespace LiveSense.Common.Converters
{
    public abstract class SafeEnumConverter<T> : IValueConverter where T : Enum
    {
        protected SafeEnumConverter() { }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is T e) return e;
            return default(T);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is T e) return e;
            return default(T);
        }
    }
}
