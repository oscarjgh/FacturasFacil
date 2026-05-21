using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using FacturasFacil.Desktop.ViewModels;

namespace FacturasFacil.Desktop;

public class LogTipoToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is LogTipo tipo ? tipo switch
        {
            LogTipo.Exito      => new SolidColorBrush(Color.FromRgb(0x16, 0xA3, 0x4A)),
            LogTipo.Advertencia => new SolidColorBrush(Color.FromRgb(0xD9, 0x77, 0x06)),
            LogTipo.Error      => new SolidColorBrush(Color.FromRgb(0xDC, 0x26, 0x26)),
            _                  => new SolidColorBrush(Color.FromRgb(0x47, 0x55, 0x69)),
        } : Brushes.Gray;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

// Singleton para usar en XAML sin instanciar en resources
public class BooleanToVisibilityConverter : IValueConverter
{
    public static readonly BooleanToVisibilityConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is Visibility.Visible;
}
