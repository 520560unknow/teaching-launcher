using System.Diagnostics;
using System.IO;
using StartMode.Models;
using StartMode.Services.Messaging;
using StartMode.Shared.Messaging;
using StartMode.Shared.Models;

namespace StartMode.Services
{
    /// <summary>
    /// 消息门面：聚合多个消息来源，统一写入 MessageStore 并向观察者推送。
    /// 缓存、去重、未读统计与持久化都由 MessageStore 负责。
    /// </summary>
    public class MessageService : IMessageService
    {
        private readonly MessageStore _store;
        private readonly List<IMessageSource> _sources;
        private readonly List<IMessageObserver> _observers = new();

        public int UnreadCount => _store.UnreadCount;

        public MessageService(AppConfig config, IEnumerable<IMessageSource> sources)
        {
            _store = new MessageStore(
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "messages_cache.json"),
                config.MaxCachedMessages);
            _sources = sources.ToList();
            foreach (var source in _sources)
            {
                source.MessageProduced += OnMessageProduced;
            }
        }

        public void Subscribe(IMessageObserver observer)
        {
            lock (_observers)
            {
                if (!_observers.Contains(observer))
                    _observers.Add(observer);
            }
        }

        public void Unsubscribe(IMessageObserver observer)
        {
            lock (_observers)
            {
                _observers.Remove(observer);
            }
        }

        public void StartListening()
        {
            foreach (var source in _sources)
            {
                source.Start();
            }
        }

        public void StopListening()
        {
            foreach (var source in _sources)
            {
                source.Stop();
            }
            _store.Save();
        }

        public void AddMessage(StudentMessage message)
        {
            _store.Add(message);
            NotifyMessageReceived(message);
            NotifyUnreadCountChanged();
        }

        public void MarkAsRead(string messageId)
        {
            if (_store.MarkAsRead(messageId))
            {
                NotifyUnreadCountChanged();
            }
        }

        public void MarkAllAsRead()
        {
            _store.MarkAllAsRead();
            NotifyUnreadCountChanged();
        }

        public List<StudentMessage> GetMessagesByType(MessageType? type = null)
        {
            var messages = _store.Snapshot();
            if (type == null)
            {
                return messages.ToList();
            }
            return messages.Where(m => m.Type == type).ToList();
        }

        private void OnMessageProduced(object? sender, StudentMessage message)
        {
            AddMessage(message);
        }

        private void NotifyMessageReceived(StudentMessage message)
        {
            IMessageObserver[] snapshot;
            lock (_observers)
            {
                snapshot = _observers.ToArray();
            }
            foreach (var observer in snapshot)
            {
                SafeNotify(observer, () => observer.OnMessageReceived(message));
            }
        }

        private void NotifyUnreadCountChanged()
        {
            IMessageObserver[] snapshot;
            lock (_observers)
            {
                snapshot = _observers.ToArray();
            }
            var count = UnreadCount;
            foreach (var observer in snapshot)
            {
                SafeNotify(observer, () => observer.OnUnreadCountChanged(count));
            }
        }

        /// <summary>
        /// 单个观察者失败（例如窗口已关闭、界面线程已退出）不应中断消息流，
        /// 否则 WebSocket 接收循环会被一起打断。
        /// </summary>
        private static void SafeNotify(IMessageObserver observer, Action callback)
        {
            try
            {
                callback();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Message] 通知观察者 {observer.GetType().Name} 失败: {ex.Message}");
            }
        }
    }
}
