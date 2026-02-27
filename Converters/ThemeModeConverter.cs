using System.Globalization;
using System.Windows.Data;
using LogVision.Models;

namespace LogVision.Converters;

public class ThemeModeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is ThemeMode mode)
        {
            return mode switch
            {
                ThemeMode.Dark => 0,
                ThemeMode.Light => 1,
                ThemeMode.Auto => 2,
                _ => 0
            };
        }
        return 0;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int index)
        {
            return index switch
            {
                0 => ThemeMode.Dark,
                1 => ThemeMode.Light,
                2 => ThemeMode.Auto,
                _ => ThemeMode.Dark
            };
        }
        return ThemeMode.Dark;
    }
}
