using System.Globalization;

namespace CheckInApp.Converters
{
    public class BoolToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue && parameter is string param)
            {
                // Formato esperado: "TextoTrue,TextoFalse" o "TextoTrue"
                var parts = param.Split(',');

                if (parts.Length >= 2)
                {
                    return boolValue ? parts[0] : parts[1];
                }

                // Si solo hay un parámetro, devolverlo solo si es true
                return boolValue ? param : string.Empty;
            }

            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}