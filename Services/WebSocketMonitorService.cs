using System.Diagnostics;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Timers;
using StartMode.Models;
using StartMode.Shared.Protocol;
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

    public class WebSocketMonitorService : IWebSocketMonitorService
    {
        private ClientWebSocket? _ws;
        private CancellationTokenSource? _cts;
        private Timer? _heartbeatTimer;
        private Timer? _reconnectTimer;
        private int _reconnectDelay = 1;
        private readonly int[] _reconnectDelays = { 1, 2, 5, 10, 10 };

        private string _classroomId = "";
        private string _wsBaseUrl = "";
        private int _generation;

        public MonitorStatus Status { get; private set; } = MonitorStatus.Disconnected;
        public string StatusText => Status switch
        {
            MonitorStatus.Connected => "已连接",
            MonitorStatus.Connecting => "连接中...",
            MonitorStatus.Reconnecting => "重连中...",
            _ => "未连接"
        };

        public int DanmakuCount { get; private set; }
        public int CommentCount { get; private set; }
        public int OpinionCount { get; private set; }
        public int SystemCount { get; private set; }

        public event EventHandler<MonitorStatus>? StatusChanged;
        public event EventHandler<ClassroomMessageItem>? MessageReceived;
        public event EventHandler? CountsChanged;

        public WebSocketMonitorService(AppConfig config)
        {
            _wsBaseUrl = config.WebSocketBaseUrl;
            if (string.IsNullOrWhiteSpace(_wsBaseUrl))
            {
                // 未单独配置 websocket 地址时，从 serverBaseUrl 推导，避免写死 localhost
                var serverBase = config.ServerBaseUrl;
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
            _generation++;
            _heartbeatTimer?.Stop();
            _heartbeatTimer?.Dispose();
            _heartbeatTimer = null;
            _reconnectTimer?.Stop();
            _reconnectTimer?.Dispose();
            _reconnectTimer = null;
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

            var generation = ++_generation;
            SetStatus(MonitorStatus.Connecting);

            ClientWebSocket? ws = null;
            CancellationTokenSource? cts = null;
            try
            {
                cts = new CancellationTokenSource();
                ws = new ClientWebSocket();

                var uri = WsEndpoints.StudentUri(_wsBaseUrl, _classroomId, "csharp_monitor");
                Debug.WriteLine($"[WS] 连接: {uri}");

                await ws.ConnectAsync(new Uri(uri), cts.Token);

                if (generation != _generation)
                {
                    // 连接期间被 Disconnect 或新一次连接取代，丢弃这次的结果
                    ws.Abort();
                    ws.Dispose();
                    return;
                }

                _ws = ws;
                _cts = cts;
                SetStatus(MonitorStatus.Connected);
                _reconnectDelay = 1;
                StartHeartbeat(ws, cts, generation);
                _ = ReceiveLoopAsync(ws, cts, generation);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WS] 连接失败: {ex.Message}");
                ws?.Dispose();
                if (generation == _generation)
                {
                    SetStatus(MonitorStatus.Reconnecting);
                    ScheduleReconnect(generation);
                }
            }
        }

        private async Task ReceiveLoopAsync(ClientWebSocket ws, CancellationTokenSource cts, int generation)
        {
            var buffer = new byte[8192];

            try
            {
                while (ws.State == WebSocketState.Open && !cts.IsCancellationRequested)
                {
                    var sb = new StringBuilder();
                    WebSocketReceiveResult result;

                    do
                    {
                        result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), cts.Token);

                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            Debug.WriteLine("[WS] 服务端关闭连接");
                            if (generation == _generation)
                            {
                                SetStatus(MonitorStatus.Reconnecting);
                                ScheduleReconnect(generation);
                            }
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
            catch (Exception ex)
            {
                Debug.WriteLine($"[WS] 接收异常: {ex.Message}");
                if (generation == _generation && !cts.IsCancellationRequested)
                {
                    SetStatus(MonitorStatus.Reconnecting);
                    ScheduleReconnect(generation);
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

                // 只抛事件，由订阅端自行调度到 UI 线程并维护消息列表
                MessageReceived?.Invoke(this, item);
                CountsChanged?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WS] 消息处理失败: {ex.Message}");
            }
        }

        private void StartHeartbeat(ClientWebSocket ws, CancellationTokenSource cts, int generation)
        {
            _heartbeatTimer?.Dispose();
            _heartbeatTimer = new Timer(25000);
            _heartbeatTimer.Elapsed += async (s, e) =>
            {
                if (ws.State != WebSocketState.Open || generation != _generation)
                {
                    return;
                }

                try
                {
                    var ping = Encoding.UTF8.GetBytes(WsEndpoints.PingPayload);
                    await ws.SendAsync(new ArraySegment<byte>(ping), WebSocketMessageType.Text, true, cts.Token);
                    Debug.WriteLine("[WS] 心跳发送");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[WS] 心跳失败: {ex.Message}");
                    if (generation == _generation)
                    {
                        // 心跳失败说明连接已不可用，按断线处理而不是留一个假"已连接"状态
                        _heartbeatTimer?.Stop();
                        ws.Abort();
                        SetStatus(MonitorStatus.Reconnecting);
                        ScheduleReconnect(generation);
                    }
                }
            };
            _heartbeatTimer.AutoReset = true;
            _heartbeatTimer.Start();
        }

        private void ScheduleReconnect(int generation)
        {
            _reconnectTimer?.Dispose();
            _reconnectTimer = new Timer(_reconnectDelay * 1000);
            _reconnectTimer.AutoReset = false;
            _reconnectTimer.Elapsed += async (s, e) =>
            {
                if (generation != _generation)
                {
                    return;
                }
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
