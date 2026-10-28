using System.Globalization;
using System.Windows.Data;
using Media = System.Windows.Media;
using StartMode.Models;

namespace StartMode.Converters
{
    public class UnreadToBackgroundConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isUnread && isUnread)
            {
                return new Media.SolidColorBrush(Media.Color.FromArgb(10, 33, 150, 243));
            }
            return Media.Brushes.Transparent;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class MessageTypeToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is MessageType type)
            {
                return type switch
                {
                    MessageType.Comment => new Media.SolidColorBrush(Media.Color.FromRgb(76, 175, 80)),
                    MessageType.Danmaku => new Media.SolidColorBrush(Media.Color.FromRgb(255, 152, 0)),
                    MessageType.Opinion => new Media.SolidColorBrush(Media.Color.FromRgb(156, 39, 176)),
                    _ => new Media.SolidColorBrush(Media.Color.FromRgb(158, 158, 158))
                };
            }
            return new Media.SolidColorBrush(Media.Color.FromRgb(158, 158, 158));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
