using System.Globalization;
using System.Windows.Data;

namespace OtomatikMetinGenisletici.Helpers
{
    public class BoolToStatusConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isActive)
            {
                return isActive ? "AKTIF" : "PASIF";
            }
            return "BILINMIYOR";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
