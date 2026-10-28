using System.ComponentModel;
using System.Windows;
using System.Windows.Data;
using StudentMessageBall.Models;
using StudentMessageBall.Services;

namespace StudentMessageBall.Windows;

/// <summary>
/// 学生消息展示面板窗口
/// </summary>
public partial class MessagePanelWindow : Window
{
    private List<StudentMessage> _allMessages = new();
    private ICollectionView _messagesView;

    public MessagePanelWindow()
    {
        InitializeComponent();

        _messagesView = CollectionViewSource.GetDefaultView(_allMessages);
        MessagesList.ItemsSource = _messagesView;
        MessagesList.MouseLeftButtonUp += MessagesList_MouseLeftButtonUp;

        RefreshMessages();
        MessageService.Instance.OnMessagesReceived += OnNewMessages;
    }

    private void OnNewMessages(List<StudentMessage> messages)
    {
        Dispatcher.Invoke(() => RefreshMessages());
    }

    public void RefreshMessages()
    {
        _allMessages = LocalCacheService.Instance.GetMessages()
            .OrderByDescending(m => m.Timestamp)
            .ToList();

        _messagesView = CollectionViewSource.GetDefaultView(_allMessages);
        MessagesList.ItemsSource = _messagesView;
        ApplyFilter();
    }

    private void FilterChanged(object sender, RoutedEventArgs e) => ApplyFilter();

    private void ApplyFilter()
    {
        _messagesView.Filter = item =>
        {
            var msg = item as StudentMessage;
            if (msg == null) return false;

            if (RbComment.IsChecked == true) return msg.Type == MessageType.Comment;
            if (RbDanmaku.IsChecked == true) return msg.Type == MessageType.Danmaku;
            if (RbOpinion.IsChecked == true) return msg.Type == MessageType.Opinion;
            return true; // 全部
        };
        _messagesView.Refresh();
        TxtStats.Text = $"共 {_messagesView.Cast<object>().Count()} 条消息";
    }

    private void MessagesList_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is System.Windows.Controls.ItemsControl itemsControl)
        {
            var container = itemsControl.ContainerFromElement((System.Windows.DependencyObject)e.OriginalSource)
                as System.Windows.Controls.ContentPresenter;
            if (container?.Content is StudentMessage msg && msg.Status == MessageStatus.Unread)
            {
                LocalCacheService.Instance.MarkAsRead(msg.Id);
                RefreshMessages();
            }
        }
    }

    private void BtnMarkAllRead_Click(object sender, RoutedEventArgs e)
    {
        LocalCacheService.Instance.MarkAllAsRead();
        RefreshMessages();
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();

    private void Window_Closing(object sender, CancelEventArgs e)
    {
        MessageService.Instance.OnMessagesReceived -= OnNewMessages;
    }
}

#region Converters

/// <summary>
/// 消息类型转中文名
/// </summary>
public class MessageTypeConverter : System.Windows.Data.IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        if (value is MessageType type)
            return type.ToChineseName();
        return "未知";
    }

    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// 字符串非空转可见
/// </summary>
public class StringToVisibilityConverter : System.Windows.Data.IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        return string.IsNullOrEmpty(value as string) ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        => throw new NotImplementedException();
}

#endregion