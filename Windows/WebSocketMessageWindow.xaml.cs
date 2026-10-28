using System.ComponentModel;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using StartMode.Models;
using StartMode.Services;
using MediaColor = System.Windows.Media.Color;

namespace StartMode.Windows
{
    public partial class WebSocketMessageWindow : Window
    {
        private readonly WebSocketMonitorService _monitor;
        private string _currentFilter = "";
        private ICollectionView? _messagesView;

        public WebSocketMessageWindow()
        {
            InitializeComponent();
            _monitor = App.WsMonitorService;

            // 绑定消息列表
            _messagesView = CollectionViewSource.GetDefaultView(_monitor.Messages);
            _messagesView.Filter = FilterMessage;
            MessageList.ItemsSource = _messagesView;

            // 监听事件
            _monitor.StatusChanged += OnStatusChanged;
            _monitor.CountsChanged += OnCountsChanged;
            _monitor.MessageReceived += OnMessageReceived;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateStatusUI(_monitor.Status);
            UpdateCounts();
        }

        private void Connect_Click(object sender, RoutedEventArgs e)
        {
            var classroomId = ClassroomIdBox.Text.Trim();
            if (string.IsNullOrEmpty(classroomId))
            {
                System.Windows.MessageBox.Show("请输入课堂 ID", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _monitor.Connect(classroomId);
        }

        private void Disconnect_Click(object sender, RoutedEventArgs e)
        {
            _monitor.Disconnect();
        }

        private void OnStatusChanged(object? sender, MonitorStatus status)
        {
            Dispatcher.BeginInvoke(() => UpdateStatusUI(status));
        }

        private void UpdateStatusUI(MonitorStatus status)
        {
            switch (status)
            {
                case MonitorStatus.Connected:
                    StatusBadge.Background = new SolidColorBrush(MediaColor.FromRgb(0x4C, 0xAF, 0x50));
                    StatusText.Text = "已连接";
                    ConnectBtn.Visibility = Visibility.Collapsed;
                    DisconnectBtn.Visibility = Visibility.Visible;
                    ClassroomIdBox.IsEnabled = false;
                    break;
                case MonitorStatus.Connecting:
                    StatusBadge.Background = new SolidColorBrush(MediaColor.FromRgb(0xFF, 0x98, 0x00));
                    StatusText.Text = "连接中...";
                    ConnectBtn.Visibility = Visibility.Collapsed;
                    DisconnectBtn.Visibility = Visibility.Collapsed;
                    ClassroomIdBox.IsEnabled = false;
                    break;
                case MonitorStatus.Reconnecting:
                    StatusBadge.Background = new SolidColorBrush(MediaColor.FromRgb(0xFF, 0x57, 0x22));
                    StatusText.Text = "重连中...";
                    ConnectBtn.Visibility = Visibility.Collapsed;
                    DisconnectBtn.Visibility = Visibility.Visible;
                    ClassroomIdBox.IsEnabled = false;
                    break;
                default:
                    StatusBadge.Background = new SolidColorBrush(MediaColor.FromRgb(0x66, 0x66, 0x66));
                    StatusText.Text = "未连接";
                    ConnectBtn.Visibility = Visibility.Visible;
                    DisconnectBtn.Visibility = Visibility.Collapsed;
                    ClassroomIdBox.IsEnabled = true;
                    break;
            }

            BottomStatus.Text = status == MonitorStatus.Connected
                ? $"已连接课堂 {_monitor.StatusText}"
                : _monitor.StatusText;
        }

        private void OnCountsChanged(object? sender, EventArgs e)
        {
            Dispatcher.BeginInvoke(UpdateCounts);
        }

        private void UpdateCounts()
        {
            var total = _monitor.DanmakuCount + _monitor.CommentCount + _monitor.OpinionCount + _monitor.SystemCount;
            FilterAll.Content = $"全部 {total}";
            FilterDanmaku.Content = $"弹幕 {_monitor.DanmakuCount}";
            FilterComment.Content = $"评论 {_monitor.CommentCount}";
            FilterOpinion.Content = $"观点 {_monitor.OpinionCount}";

            BottomStatus.Text = _monitor.IsConnected
                ? $"已连接 | 共 {total} 条消息"
                : _monitor.StatusText;
        }

        private void OnMessageReceived(object? sender, ClassroomMessageItem item)
        {
            Dispatcher.BeginInvoke(() =>
            {
                _messagesView?.Refresh();
                if (MessageList.Items.Count > 0)
                    MessageList.ScrollIntoView(MessageList.Items[0]);
            });
        }

        private void Filter_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement btn && btn.Tag is string tag)
            {
                _currentFilter = tag;
                _messagesView?.Refresh();
                if (!string.IsNullOrEmpty(tag) && Enum.TryParse<MessageCategory>(tag, out var cat))
                    _monitor.ResetCounts(cat);
                else
                    _monitor.ResetCounts();
            }
        }

        private bool FilterMessage(object item)
        {
            if (item is not ClassroomMessageItem msg) return false;
            if (string.IsNullOrEmpty(_currentFilter)) return true;
            return msg.Category.ToString() == _currentFilter;
        }

        private void TitleBar_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            DragMove();
        }

        private void Minimize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Hide();
        }

        private void Window_Closing(object? sender, CancelEventArgs e)
        {
            e.Cancel = true;
            Hide();
        }
    }
}
