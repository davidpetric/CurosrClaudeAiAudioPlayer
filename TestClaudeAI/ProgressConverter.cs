using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace TestClaudeAI
{
    public class ProgressConverter : IValueConverter
    {
        public static readonly ProgressConverter Instance = new ProgressConverter();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double doubleValue)
            {
                return doubleValue / 100.0;
            }
            return 0.0;
        }

        public object ConvertBack(
            object value,
            Type targetType,
            object parameter,
            CultureInfo culture
        )
        {
            if (value is double doubleValue)
            {
                return doubleValue * 100.0;
            }
            return 0.0;
        }
    }
}
