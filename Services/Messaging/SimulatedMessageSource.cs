using StartMode.Models;
using StartMode.Shared.Models;

namespace StartMode.Services.Messaging
{
    /// <summary>
    /// 演示用的随机消息来源，仅在配置 enableSimulatedMessages 打开时产生消息。
    /// </summary>
    public sealed class SimulatedMessageSource : IMessageSource
    {
        private static readonly string[] Contents =
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

        private readonly AppConfig _config;
        private CancellationTokenSource? _cts;
        private Task? _pollingTask;

        public event EventHandler<StudentMessage>? MessageProduced;

        public SimulatedMessageSource(AppConfig config)
        {
            _config = config;
        }

        public void Start()
        {
            if (!_config.EnableSimulatedMessages)
            {
                return;
            }

            _cts = new CancellationTokenSource();
            _pollingTask = Task.Run(async () => await PollAsync(_cts.Token));
        }

        public void Stop()
        {
            _cts?.Cancel();
            try
            {
                _pollingTask?.Wait(TimeSpan.FromSeconds(2));
            }
            catch (AggregateException) { }
        }

        private async Task PollAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    SimulateReceive();
                    await Task.Delay(TimeSpan.FromSeconds(_config.PollingIntervalSeconds), cancellationToken);
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

        private void SimulateReceive()
        {
            if (!_config.EnableSimulatedMessages)
            {
                return;
            }

            var random = Random.Shared;
            if (random.Next(10) >= 3)
            {
                return;
            }

            var message = new StudentMessage
            {
                StudentId = $"student_{random.Next(1, 20)}",
                StudentName = $"学生{random.Next(1, 20)}号",
                Type = (MessageType)random.Next(3),
                Content = Contents[random.Next(Contents.Length)],
                Source = "课堂互动",
                Timestamp = DateTime.Now
            };

            MessageProduced?.Invoke(this, message);
        }
    }
}
