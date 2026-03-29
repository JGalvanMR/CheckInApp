using System.Globalization;

namespace CheckInApp.Converters
{
    public class BotonActivoConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int activo && parameter is string param)
            {
                if (int.TryParse(param, out int index))
                {
                    //return activo == index ? "#2196F3" : "#9E9E9E";
                    return activo == index
                    ? Color.FromArgb("#1A4B7A") // Azul corporativo
                    : Color.FromArgb("#9CA3AF"); // Gris corporativo
                }
            }
            //return "#9E9E9E";
            return Color.FromArgb("#9CA3AF");
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}