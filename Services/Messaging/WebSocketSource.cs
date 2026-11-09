using StartMode.Models;
using StartMode.Shared.Models;

namespace StartMode.Services.Messaging
{
    /// <summary>
    /// 真实消息来源：把课堂 WebSocket 监听服务收到的消息转成统一的学生消息模型。
    /// </summary>
    public sealed class WebSocketSource : IMessageSource
    {
        private readonly IWebSocketMonitorService _monitor;

        public event EventHandler<StudentMessage>? MessageProduced;

        public WebSocketSource(IWebSocketMonitorService monitor)
        {
            _monitor = monitor;
        }

        public void Start()
        {
            _monitor.MessageReceived += OnMonitorMessageReceived;
        }

        public void Stop()
        {
            _monitor.MessageReceived -= OnMonitorMessageReceived;
        }

        private void OnMonitorMessageReceived(object? sender, ClassroomMessageItem item)
        {
            MessageType? type = item.Category switch
            {
                MessageCategory.Danmaku => MessageType.Danmaku,
                MessageCategory.Comment => MessageType.Comment,
                MessageCategory.Opinion => MessageType.Opinion,
                _ => null
            };

            // 系统消息（上线、心跳等）不进入学生消息列表
            if (type == null)
            {
                return;
            }

            var senderName = item.StudentName;
            if (string.IsNullOrWhiteSpace(senderName) && !string.IsNullOrWhiteSpace(item.TeacherName))
            {
                senderName = $"教师 {item.TeacherName}";
            }

            MessageProduced?.Invoke(this, new StudentMessage
            {
                Id = item.Id,
                StudentId = item.StudentId,
                StudentName = senderName,
                Type = type.Value,
                Content = item.Content,
                Timestamp = item.Timestamp,
                Source = "课堂互动",
                Status = MessageStatus.Unread
            });
        }
    }
}
