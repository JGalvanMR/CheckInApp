using System.Globalization;

namespace CheckInApp.Converters
{
    public class BoolToIDColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool asistio)
            {
                //return asistio ? Color.FromArgb("#4CAF50") : Color.FromArgb("#757575");
                return asistio
                ? Color.FromArgb("#15803D") // Verde corporativo
                : Color.FromArgb("#6B7280"); // Gris corporativo
            }
            //return Color.FromArgb("#757575");
            return Color.FromArgb("#6B7280");
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}