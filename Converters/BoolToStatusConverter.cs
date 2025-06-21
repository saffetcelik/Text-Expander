using System;
using System.Globalization;
using System.Windows.Data;

namespace OtomatikMetinGenisletici.Converters
{
    public class BoolToStatusConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return boolValue ? "🟢 Aktif" : "🔴 Pasif";
            }
            return "❓ Bilinmiyor";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
