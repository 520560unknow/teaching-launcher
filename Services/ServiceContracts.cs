using StartMode.Models;
using StartMode.Services.Messaging;
using StartMode.Shared.Models;

namespace StartMode.Services
{
    /// <summary>UI 侧需要的 Java 网页服务能力。</summary>
    public interface IJavaAppService
    {
        bool IsStarting { get; }
        Task<bool> IsServerHealthyAsync();
        Task<bool> EnsureStartedAsync();
        Task<bool> StartAsync();
        Task<(bool ok, string message)> StopAsync();
        void StopJavaApp();
    }

    /// <summary>UI 侧需要的本地模型服务能力。</summary>
    public interface ILlamaService
    {
        bool IsStarting { get; }
        int Port { get; }
        Task<bool> IsRunningAsync();
        Task<(bool ok, string message)> StartAsync();
        Task<(bool ok, string message)> StopAsync();
        void StopImmediately();
    }

    /// <summary>UI 侧需要的消息缓存能力。</summary>
    public interface IMessageService
    {
        void Subscribe(IMessageObserver observer);
        void Unsubscribe(IMessageObserver observer);

        int UnreadCount { get; }
        List<StudentMessage> GetMessagesByType(MessageType? type = null);
        void MarkAllAsRead();
        void StartListening();
        void StopListening();
    }

    /// <summary>UI 侧需要的课堂 WebSocket 监听能力（服务层不依赖 WPF，事件由订阅端调度到 UI 线程）。</summary>
    public interface IWebSocketMonitorService : IDisposable
    {
        MonitorStatus Status { get; }
        string StatusText { get; }
        int DanmakuCount { get; }
        int CommentCount { get; }
        int OpinionCount { get; }
        int SystemCount { get; }
        bool IsConnected { get; }

        event EventHandler<MonitorStatus>? StatusChanged;
        event EventHandler<ClassroomMessageItem>? MessageReceived;
        event EventHandler? CountsChanged;

        void Connect(string classroomId);
        void Disconnect();
        void ResetCounts(MessageCategory? category = null);
    }
}
