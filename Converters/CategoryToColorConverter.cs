using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using StartMode.Models;
using MediaColor = System.Windows.Media.Color;

namespace StartMode.Windows
{
    public class CategoryToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is MessageCategory cat)
            {
                return cat switch
                {
                    MessageCategory.Danmaku => new SolidColorBrush(MediaColor.FromRgb(0x64, 0xB5, 0xF6)),
                    MessageCategory.Comment => new SolidColorBrush(MediaColor.FromRgb(0x81, 0xC7, 0x84)),
                    MessageCategory.Opinion => new SolidColorBrush(MediaColor.FromRgb(0xFF, 0xB7, 0x4D)),
                    _ => new SolidColorBrush(MediaColor.FromRgb(0x95, 0x75, 0xCD))
                };
            }
            return new SolidColorBrush(MediaColor.FromRgb(0x66, 0x66, 0x66));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class StringToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return string.IsNullOrEmpty(value as string) ? System.Windows.Visibility.Collapsed : System.Windows.Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
