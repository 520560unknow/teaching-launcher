using System.Windows;
using System.Windows.Controls;
using StartMode.Converters;
using StartMode.Models;
using Media = System.Windows.Media;

namespace StartMode.Windows
{
    public partial class MessagePanelWindow : Window
    {
        private MessageType? _currentFilter = null;

        public MessagePanelWindow()
        {
            InitializeComponent();
            SetupMessageService();
        }

        private void SetupMessageService()
        {
            var messageService = App.MessageService;
            messageService.MessageReceived += OnMessageReceived;
            messageService.UnreadCountChanged += OnUnreadCountChanged;
        }

        private void OnMessageReceived(object? sender, StudentMessage message)
        {
            Dispatcher.Invoke(() =>
            {
                RefreshMessageList();
            });
        }

        private void OnUnreadCountChanged(object? sender, int count)
        {
            Dispatcher.Invoke(() =>
            {
                UpdateStatus();
            });
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            RefreshMessageList();
        }

        private void RefreshMessageList()
        {
            var messageService = App.MessageService;
            var messages = messageService.GetMessagesByType(_currentFilter);
            
            var displayMessages = messages.Select(m => new MessageDisplayModel
            {
                Id = m.Id,
                StudentName = m.StudentName,
                Content = m.Content,
                TypeName = m.TypeName,
                TypeColor = new MessageTypeToColorConverter().Convert(m.Type, typeof(Media.Brush), null, System.Globalization.CultureInfo.CurrentCulture) as Media.Brush ?? Media.Brushes.Gray,
                TimeDisplay = m.TimeDisplay,
                Source = m.Source,
                IsUnread = m.Status == MessageStatus.Unread
            }).Reverse().ToList();
            
            MessageList.ItemsSource = displayMessages;
            
            EmptyState.Visibility = displayMessages.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            
            UpdateStatus();
        }

        private void UpdateStatus()
        {
            var messageService = App.MessageService;
            var totalCount = messageService.GetMessagesByType(_currentFilter).Count;
            var unreadCount = messageService.UnreadCount;
            
            StatusText.Text = $"共 {totalCount} 条消息" + (unreadCount > 0 ? $"，{unreadCount} 条未读" : "");
        }

        private void Filter_Click(object sender, RoutedEventArgs e)
        {
            if (sender == FilterAll)
                _currentFilter = null;
            else if (sender == FilterComment)
                _currentFilter = MessageType.Comment;
            else if (sender == FilterDanmaku)
                _currentFilter = MessageType.Danmaku;
            else if (sender == FilterOpinion)
                _currentFilter = MessageType.Opinion;
            
            RefreshMessageList();
        }

        private void MarkAllRead_Click(object sender, RoutedEventArgs e)
        {
            App.MessageService.MarkAllAsRead();
            RefreshMessageList();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Window_Deactivated(object sender, EventArgs e)
        {
            Close();
        }
    }

    public class MessageDisplayModel
    {
        public string Id { get; set; } = string.Empty;
        public string StudentName { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string TypeName { get; set; } = string.Empty;
        public Media.Brush TypeColor { get; set; } = Media.Brushes.Gray;
        public string TimeDisplay { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public bool IsUnread { get; set; }
    }
}
