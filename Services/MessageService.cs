using System.Collections.ObjectModel;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using StartMode.Models;

namespace StartMode.Services
{
    public class MessageService
    {
        private readonly ObservableCollection<StudentMessage> _messages = new();
        private readonly string _cachePath;
        private CancellationTokenSource? _cancellationTokenSource;
        private Task? _pollingTask;

        public event EventHandler<StudentMessage>? MessageReceived;
        public event EventHandler<int>? UnreadCountChanged;

        public ObservableCollection<StudentMessage> Messages => _messages;

        public int UnreadCount => _messages.Count(m => m.Status == MessageStatus.Unread);

        public MessageService()
        {
            _cachePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "messages_cache.json");
            LoadCachedMessages();
        }

        public void StartListening()
        {
            // 演示用的随机消息默认关闭，避免界面上出现虚构的学生消息
            if (!App.ConfigService.Config.EnableSimulatedMessages)
                return;

            _cancellationTokenSource = new CancellationTokenSource();

            _pollingTask = Task.Run(async () => await PollMessagesAsync(_cancellationTokenSource.Token));
        }

        public void StopListening()
        {
            _cancellationTokenSource?.Cancel();
            _pollingTask?.Wait(TimeSpan.FromSeconds(2));
            SaveCachedMessages();
        }

        private async Task PollMessagesAsync(CancellationToken cancellationToken)
        {
            var config = App.ConfigService.Config;

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await SimulateMessageReceiveAsync();
                    
                    await Task.Delay(TimeSpan.FromSeconds(config.PollingIntervalSeconds), cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"轮询消息失败: {ex.Message}");
                    await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
                }
            }
        }

        private async Task SimulateMessageReceiveAsync()
        {
            // 仅在显式开启模拟模式时生成示例消息
            if (!App.ConfigService.Config.EnableSimulatedMessages)
                return;

            await Task.CompletedTask;
            
            var random = new Random();
            if (random.Next(10) < 3)
            {
                var message = new StudentMessage
                {
                    StudentId = $"student_{random.Next(1, 20)}",
                    StudentName = $"学生{random.Next(1, 20)}号",
                    Type = (MessageType)random.Next(3),
                    Content = GetRandomMessageContent(),
                    Source = "课堂互动",
                    Timestamp = DateTime.Now
                };

                AddMessage(message);
            }
        }

        private string GetRandomMessageContent()
        {
            var contents = new[]
            {
                "老师讲得太好了！",
                "这个知识点我明白了",
                "能再解释一下吗？",
                "我有不同看法",
                "同意！",
                "这个问题很有意思",
                "我来补充一下...",
                "请问这是重点吗？",
                "明白了，谢谢老师",
                "我觉得这样理解对吗？"
            };
            return contents[Random.Shared.Next(contents.Length)];
        }

        public void AddMessage(StudentMessage message)
        {
            _messages.Add(message);
            MessageReceived?.Invoke(this, message);
            UnreadCountChanged?.Invoke(this, UnreadCount);
        }

        public void MarkAsRead(string messageId)
        {
            var message = _messages.FirstOrDefault(m => m.Id == messageId);
            if (message != null)
            {
                message.Status = MessageStatus.Read;
                UnreadCountChanged?.Invoke(this, UnreadCount);
            }
        }

        public void MarkAllAsRead()
        {
            foreach (var message in _messages.Where(m => m.Status == MessageStatus.Unread))
            {
                message.Status = MessageStatus.Read;
            }
            UnreadCountChanged?.Invoke(this, UnreadCount);
        }

        public List<StudentMessage> GetMessagesByType(MessageType? type = null)
        {
            if (type == null)
                return _messages.ToList();
            return _messages.Where(m => m.Type == type).ToList();
        }

        private void LoadCachedMessages()
        {
            try
            {
                if (File.Exists(_cachePath))
                {
                    var json = File.ReadAllText(_cachePath);
                    var cached = JsonSerializer.Deserialize<List<StudentMessage>>(json);
                    if (cached != null)
                    {
                        foreach (var msg in cached.Take(App.ConfigService.Config.MaxCachedMessages))
                        {
                            _messages.Add(msg);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"加载缓存消息失败: {ex.Message}");
            }
        }

        public void SaveCachedMessages()
        {
            try
            {
                var messagesToSave = _messages
                    .TakeLast(App.ConfigService.Config.MaxCachedMessages)
                    .ToList();
                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(messagesToSave, options);
                File.WriteAllText(_cachePath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"保存缓存消息失败: {ex.Message}");
            }
        }
    }
}
