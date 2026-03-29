using System.Globalization;

namespace CheckInApp.Converters
{
    public class InicialesConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string nombre && !string.IsNullOrWhiteSpace(nombre))
            {
                var partes = nombre.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (partes.Length >= 2)
                {
                    return $"{partes[0][0]}{partes[^1][0]}".ToUpper();
                }
                return nombre.Length >= 2 ? nombre.Substring(0, 2).ToUpper() : nombre.ToUpper();
            }
            return "??";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}