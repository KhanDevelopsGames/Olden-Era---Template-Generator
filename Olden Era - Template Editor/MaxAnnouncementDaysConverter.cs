using System;
using System.Globalization;
using System.Windows.Data;

namespace Olden_Era___Template_Editor
{
    // Tournament Announcement Days must be less than First Tournament Day and Tournament Interval!
    public class MaxAnnouncementDaysConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values == null || values.Length != 2)
                return 1.0;

            if (!TryToDouble(values[0], out double a) || !TryToDouble(values[1], out double b))
                return 1.0;

            double days = Math.Min(a, b) - 1.0;
            if (double.IsNaN(days) || days < 1.0)
                days = 1.0;

            return days;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }

        private static bool TryToDouble(object? value, out double result)
        {
            result = 0;
            if (value == null) return false;
            if (value is double d) { result = d; return true; }
            if (value is float f) { result = f; return true; }
            if (value is int i) { result = i; return true; }
            if (double.TryParse(value.ToString(), out d)) { result = d; return true; }
            return false;
        }
    }
}
