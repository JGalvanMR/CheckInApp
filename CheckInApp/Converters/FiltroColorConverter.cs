using System.Globalization;

namespace CheckInApp.Converters
{
    public class FiltroColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string filtroSeleccionado && parameter is string filtroBoton)
            {
                // Si el botón coincide con el filtro seleccionado, ponerlo activo
                if (filtroSeleccionado == filtroBoton)
                {
                    return filtroBoton switch
                    {
                        //"Todos" => Color.FromArgb("#2196F3"),     // Azul
                        //"Presentes" => Color.FromArgb("#4CAF50"), // Verde
                        //"Ausentes" => Color.FromArgb("#F44336"),  // Rojo
                        //_ => Color.FromArgb("#757575")            // Gris por defecto
                        "Todos" => Color.FromArgb("#1A4B7A"),      // Azul corporativo
                        "Presentes" => Color.FromArgb("#15803D"), // Verde corporativo
                        "Ausentes" => Color.FromArgb("#DC2626"),  // Rojo corporativo
                        _ => Color.FromArgb("#6B7280")
                    };
                }
                else
                {
                    //return Color.FromArgb("#BDBDBD"); // Gris claro cuando no está activo
                    return Color.FromArgb("#D1D5DB"); // Gris claro corporativo
                }
            }

            //return Color.FromArgb("#757575"); // Color por defecto
            return Color.FromArgb("#6B7280");
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}