using System.Globalization;

namespace CheckInApp.Converters
{
    public class BoolToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isTrue = value is bool b && b;
            string type = parameter as string;

            return type switch
            {
                "Background" => isTrue ? Color.FromArgb("#DCFCE7") : Color.FromArgb("#FFEDD5"),
                "Indicator" => isTrue ? Color.FromArgb("#22C55E") : Color.FromArgb("#F97316"),
                "Text" => isTrue ? Color.FromArgb("#166534") : Color.FromArgb("#9A3412"),
                _ => Color.FromArgb("#CBD5E1")
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}