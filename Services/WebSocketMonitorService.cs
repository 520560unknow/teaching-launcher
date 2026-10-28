using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Timers;
using StartMode.Models;
using Timer = System.Timers.Timer;

namespace StartMode.Services
{
    public enum MonitorStatus
    {
        Disconnected,
        Connecting,
        Connected,
        Reconnecting
    }

    public class WebSocketMonitorService : IDisposable
    {
        private ClientWebSocket? _ws;
        private CancellationTokenSource? _cts;
        private Timer? _heartbeatTimer;
        private Timer? _reconnectTimer;
        private int _reconnectDelay = 1;
        private readonly int[] _reconnectDelays = { 1, 2, 5, 10, 10 };

        private string _classroomId = "";
        private string _wsBaseUrl = "";

        public MonitorStatus Status { get; private set; } = MonitorStatus.Disconnected;
        public string StatusText => Status switch
        {
            MonitorStatus.Connected => "已连接",
            MonitorStatus.Connecting => "连接中...",
            MonitorStatus.Reconnecting => "重连中...",
            _ => "未连接"
        };

        public ObservableCollection<ClassroomMessageItem> Messages { get; } = new();
        public int DanmakuCount { get; private set; }
        public int CommentCount { get; private set; }
        public int OpinionCount { get; private set; }
        public int SystemCount { get; private set; }

        public event EventHandler<MonitorStatus>? StatusChanged;
        public event EventHandler<ClassroomMessageItem>? MessageReceived;
        public event EventHandler? CountsChanged;

        public WebSocketMonitorService()
        {
            _wsBaseUrl = App.ConfigService.Config.WebSocketBaseUrl;
            if (string.IsNullOrWhiteSpace(_wsBaseUrl))
            {
                // 未单独配置 websocket 地址时，从 serverBaseUrl 推导，避免写死 localhost
                var serverBase = App.ConfigService.Config.ServerBaseUrl;
                if (!string.IsNullOrWhiteSpace(serverBase))
                    _wsBaseUrl = serverBase.Replace("http://", "ws://").Replace("https://", "wss://");
            }
        }

        public bool IsConnected => Status == MonitorStatus.Connected;

        public void Connect(string classroomId)
        {
            if (string.IsNullOrWhiteSpace(classroomId))
                return;

            _classroomId = classroomId.Trim();
            Disconnect();
            _ = ConnectAsync();
        }

        public void Disconnect()
        {
            _heartbeatTimer?.Stop();
            _reconnectTimer?.Stop();
            _cts?.Cancel();

            try
            {
                if (_ws?.State == WebSocketState.Open || _ws?.State == WebSocketState.Connecting)
                {
                    _ws?.Abort();
                }
                _ws?.Dispose();
                _ws = null;
            }
            catch { }

            SetStatus(MonitorStatus.Disconnected);
        }

        private async Task ConnectAsync()
        {
            if (string.IsNullOrWhiteSpace(_wsBaseUrl))
            {
                Debug.WriteLine("[WS] 未配置 webSocketBaseUrl，跳过连接");
                SetStatus(MonitorStatus.Disconnected);
                return;
            }

            SetStatus(MonitorStatus.Connecting);

            try
            {
                _cts = new CancellationTokenSource();
                _ws = new ClientWebSocket();

                var encodedClassroomId = Uri.EscapeDataString(_classroomId);
                var uri = $"{_wsBaseUrl}/ws/student/{encodedClassroomId}?studentId=csharp_monitor";
                Debug.WriteLine($"[WS] 连接: {uri}");

                await _ws.ConnectAsync(new Uri(uri), _cts.Token);

                SetStatus(MonitorStatus.Connected);
                _reconnectDelay = 1;
                StartHeartbeat();
                _ = ReceiveLoopAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WS] 连接失败: {ex.Message}");
                SetStatus(MonitorStatus.Reconnecting);
                ScheduleReconnect();
            }
        }

        private async Task ReceiveLoopAsync()
        {
            var buffer = new byte[8192];

            try
            {
                while (_ws?.State == WebSocketState.Open && _cts?.IsCancellationRequested == false)
                {
                    var sb = new StringBuilder();
                    WebSocketReceiveResult result;

                    do
                    {
                        result = await _ws.ReceiveAsync(new ArraySegment<byte>(buffer), _cts!.Token);

                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            Debug.WriteLine("[WS] 服务端关闭连接");
                            SetStatus(MonitorStatus.Reconnecting);
                            ScheduleReconnect();
                            return;
                        }

                        sb.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                    }
                    while (!result.EndOfMessage);

                    var text = sb.ToString();
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        ProcessMessage(text);
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (WebSocketException ex)
            {
                Debug.WriteLine($"[WS] 接收异常: {ex.Message}");
                if (!_cts?.IsCancellationRequested ?? false)
                {
                    SetStatus(MonitorStatus.Reconnecting);
                    ScheduleReconnect();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WS] 未知异常: {ex.Message}");
                if (!_cts?.IsCancellationRequested ?? false)
                {
                    SetStatus(MonitorStatus.Reconnecting);
                    ScheduleReconnect();
                }
            }
        }

        private void ProcessMessage(string json)
        {
            try
            {
                var item = ClassroomMessageItem.Parse(json);
                item.ClassroomId = _classroomId;

                // 更新计数
                switch (item.Category)
                {
                    case MessageCategory.Danmaku: DanmakuCount++; break;
                    case MessageCategory.Comment: CommentCount++; break;
                    case MessageCategory.Opinion: OpinionCount++; break;
                    default: SystemCount++; break;
                }

                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    Messages.Insert(0, item);
                    // 限制最大条数
                    while (Messages.Count > 500)
                        Messages.RemoveAt(Messages.Count - 1);
                });

                MessageReceived?.Invoke(this, item);
                CountsChanged?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WS] 消息处理失败: {ex.Message}");
            }
        }

        private void StartHeartbeat()
        {
            _heartbeatTimer?.Dispose();
            _heartbeatTimer = new Timer(25000);
            _heartbeatTimer.Elapsed += async (s, e) =>
            {
                if (_ws?.State == WebSocketState.Open)
                {
                    try
                    {
                        var ping = Encoding.UTF8.GetBytes("{\"type\":\"PING\"}");
                        await _ws.SendAsync(new ArraySegment<byte>(ping), WebSocketMessageType.Text, true, _cts!.Token);
                        Debug.WriteLine("[WS] 心跳发送");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[WS] 心跳失败: {ex.Message}");
                    }
                }
            };
            _heartbeatTimer.AutoReset = true;
            _heartbeatTimer.Start();
        }

        private void ScheduleReconnect()
        {
            _reconnectTimer?.Dispose();
            _reconnectTimer = new Timer(_reconnectDelay * 1000);
            _reconnectTimer.AutoReset = false;
            _reconnectTimer.Elapsed += async (s, e) =>
            {
                Debug.WriteLine($"[WS] {_reconnectDelay}秒后重连...");
                await ConnectAsync();
            };
            _reconnectTimer.Start();

            // 递增重连间隔
            var idx = Array.IndexOf(_reconnectDelays, _reconnectDelay);
            if (idx < _reconnectDelays.Length - 1)
                _reconnectDelay = _reconnectDelays[idx + 1];
        }

        private void SetStatus(MonitorStatus status)
        {
            Status = status;
            StatusChanged?.Invoke(this, status);
        }

        public void ResetCounts(MessageCategory? category = null)
        {
            if (category == null || category == MessageCategory.Danmaku) DanmakuCount = 0;
            if (category == null || category == MessageCategory.Comment) CommentCount = 0;
            if (category == null || category == MessageCategory.Opinion) OpinionCount = 0;
            if (category == null || category == MessageCategory.System) SystemCount = 0;
            CountsChanged?.Invoke(this, EventArgs.Empty);
        }

        public void Dispose()
        {
            Disconnect();
            _heartbeatTimer?.Dispose();
            _reconnectTimer?.Dispose();
        }
    }
}
