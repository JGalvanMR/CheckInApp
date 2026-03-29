using System.Globalization;

namespace CheckInApp.Converters
{
    public class BoolToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                //return boolValue ? Color.FromArgb("#C8E6C9") : Color.FromArgb("#FFECB3");
                return boolValue
                ? Color.FromArgb("#E8F5E9") // Verde corporativo claro
                : Color.FromArgb("#FFF3E0"); // Naranja corporativo claro
            }
            //return Color.FromArgb("#FFECB3");
            return Color.FromArgb("#FFECB3");
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}