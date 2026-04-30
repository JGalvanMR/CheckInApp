using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CheckInApp.Converters
{
    public class BoolToIconConverter : IValueConverter
    {
        public string TrueIcon { get; set; }
        public string FalseIcon { get; set; }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b)
                return b ? TrueIcon : FalseIcon;

            return FalseIcon;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
