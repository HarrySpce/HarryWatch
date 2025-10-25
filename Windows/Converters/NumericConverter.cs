using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace Harry.Windows.Converters
{
    public class NumericConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int i)     return i.ToString();
            if (value is uint u)    return u.ToString();
            if (value is long l)    return l.ToString();
            if (value is ulong r)   return r.ToString();
            if (value is float f)   return f.ToString();
            if (value is double d)  return d.ToString();
            return value.GetType().ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
