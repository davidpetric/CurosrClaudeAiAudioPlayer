using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace TestClaudeAI;

public class BooleanToStringConverter : IValueConverter
{
    public static readonly BooleanToStringConverter Instance = new BooleanToStringConverter();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue && parameter is string stringParameter)
        {
            var options = stringParameter.Split('|');
            return boolValue
                ? options[0]
                : options.Length > 1
                    ? options[1]
                    : options[0];
        }
        return value;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
