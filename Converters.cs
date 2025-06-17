using System;
using System.Globalization;
using System.Windows.Data;

namespace UchPR
{
    /// <summary>
    /// Конвертер для определения положительных значений
    /// </summary>
    public class PositiveConverter : IValueConverter
    {
        public static readonly PositiveConverter Instance = new PositiveConverter();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                if (value is decimal decimalValue)
                    return decimalValue > 0;

                if (value is int intValue)
                    return intValue > 0;

                if (value is double doubleValue)
                    return doubleValue > 0;

                if (value is float floatValue)
                    return floatValue > 0;

                // Попытка конвертации строки в число
                if (value is string stringValue && decimal.TryParse(stringValue, out decimal parsedValue))
                    return parsedValue > 0;

                return false;
            }
            catch
            {
                return false;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException("PositiveConverter не поддерживает обратную конвертацию");
        }
    }

    /// <summary>
    /// Конвертер для определения отрицательных значений
    /// </summary>
    public class NegativeConverter : IValueConverter
    {
        public static readonly NegativeConverter Instance = new NegativeConverter();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                if (value is decimal decimalValue)
                    return decimalValue < 0;

                if (value is int intValue)
                    return intValue < 0;

                if (value is double doubleValue)
                    return doubleValue < 0;

                if (value is float floatValue)
                    return floatValue < 0;

                // Попытка конвертации строки в число
                if (value is string stringValue && decimal.TryParse(stringValue, out decimal parsedValue))
                    return parsedValue < 0;

                return false;
            }
            catch
            {
                return false;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException("NegativeConverter не поддерживает обратную конвертацию");
        }
    }
}
