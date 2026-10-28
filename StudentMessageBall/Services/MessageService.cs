using System.Net.Http;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using StudentMessageBall.Models;
using SysTimer = System.Threading.Timer;

namespace StudentMessageBall.Services;

/// <summary>
/// 消息服务 - 支持 WebSocket 实时推送和 HTTP 轮询两种模式
/// </summary>
public class MessageService : IDisposable
{
    private static readonly Lazy<MessageService> _instance = new(() => new MessageService());
    public static MessageService Instance => _instance.Value;

    private readonly HttpClient _httpClient;
    private ClientWebSocket? _webSocket;
    private CancellationTokenSource? _cts;
    private SysTimer? _pollingTimer;
    private int _reconnectAttempts;
    private bool _isConnected;

    private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public event Action<List<StudentMessage>>? OnMessagesReceived;
    public event Action<bool>? OnConnectionStateChanged;
    public event Action<string>? OnError;

    public bool IsConnected => _isConnected;

    private MessageService()
    {
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
    }

    public async Task StartAsync()
    {
        var config = ConfigService.Instance.Config.MessageService;
        _cts = new CancellationTokenSource();

        if (config.Mode.Equals("WebSocket", StringComparison.OrdinalIgnoreCase))
            await StartWebSocketAsync(_cts.Token);
        else
            StartPolling(_cts.Token);
    }

    public async Task StopAsync()
    {
        _cts?.Cancel();
        _pollingTimer?.Dispose();
        _pollingTimer = null;

        if (_webSocket?.State == WebSocketState.Open)
        {
            try { await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "关闭", CancellationToken.None); }
            catch { }
        }
        _webSocket?.Dispose();
        _webSocket = null;
        UpdateConnectionState(false);
    }

    #region WebSocket

    private async Task StartWebSocketAsync(CancellationToken ct)
    {
        var config = ConfigService.Instance.Config.MessageService;
        while (!ct.IsCancellationRequested && _reconnectAttempts < config.MaxReconnectAttempts)
        {
            try
            {
                _webSocket?.Dispose();
                _webSocket = new ClientWebSocket();
                await _webSocket.ConnectAsync(new Uri(config.WebSocketUrl), ct);
                UpdateConnectionState(true);
                _reconnectAttempts = 0;
                await ReceiveWebSocketMessagesAsync(ct);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                _reconnectAttempts++;
                OnError?.Invoke($"WebSocket 连接失败 (尝试 {_reconnectAttempts}/{config.MaxReconnectAttempts}): {ex.Message}");
                UpdateConnectionState(false);
                await Task.Delay(GetReconnectDelay(), ct);
            }
        }
    }

    private async Task ReceiveWebSocketMessagesAsync(CancellationToken ct)
    {
        var buffer = new byte[8192];
        while (_webSocket?.State == WebSocketState.Open && !ct.IsCancellationRequested)
        {
            try
            {
                var result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
                if (result.MessageType == WebSocketMessageType.Close) break;

                var json = Encoding.UTF8.GetString(buffer, 0, result.Count);
                var messages = ParseMessages(json);
                if (messages.Count > 0)
                {
                    LocalCacheService.Instance.AddMessages(messages);
                    OnMessagesReceived?.Invoke(messages);
                }
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                OnError?.Invoke($"接收消息异常: {ex.Message}");
                break;
            }
        }
        UpdateConnectionState(false);
    }

    #endregion

    #region HTTP Polling

    private void StartPolling(CancellationToken ct)
    {
        var interval = ConfigService.Instance.Config.MessageService.PollingIntervalSeconds;
        // 首次立即拉取
        _ = Task.Run(() => FetchMessagesPollingAsync(ct));
        _pollingTimer = new SysTimer(async _ => await FetchMessagesPollingAsync(ct), null, interval * 1000, interval * 1000);
    }

    private async Task FetchMessagesPollingAsync(CancellationToken ct)
    {
        try
        {
            var config = ConfigService.Instance.Config.MessageService;
            var lastMsg = LocalCacheService.Instance.GetLastMessage();
            var url = $"{config.ApiBaseUrl}{config.MessagesEndpoint}";

            if (lastMsg != null)
                url += $"?since={lastMsg.Timestamp:O}";

            var response = await _httpClient.GetAsync(url, ct);
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync(ct);
                var messages = ParseMessages(json);
                if (messages.Count > 0)
                {
                    LocalCacheService.Instance.AddMessages(messages);
                    OnMessagesReceived?.Invoke(messages);
                    // 重试计数归零
                    _reconnectAttempts = 0;
                }
                UpdateConnectionState(true);
            }
            else
            {
                UpdateConnectionState(false);
                OnError?.Invoke($"HTTP 请求失败: {response.StatusCode}");
            }
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            _reconnectAttempts++;
            UpdateConnectionState(false);
            OnError?.Invoke($"轮询失败: {ex.Message}");
        }
    }

    #endregion

    #region Shared

    private List<StudentMessage> ParseMessages(string json)
    {
        try
        {
            // 尝试解析为列表
            var messages = JsonSerializer.Deserialize<List<StudentMessage>>(json, _jsonOptions);
            if (messages != null) return messages;
        }
        catch
        {
            try
            {
                // 尝试解析为单个消息
                var msg = JsonSerializer.Deserialize<StudentMessage>(json, _jsonOptions);
                if (msg != null) return new List<StudentMessage> { msg };
            }
            catch { }
        }
        return new List<StudentMessage>();
    }

    private void UpdateConnectionState(bool connected)
    {
        if (_isConnected != connected)
        {
            _isConnected = connected;
            OnConnectionStateChanged?.Invoke(connected);
        }
    }

    private int GetReconnectDelay()
    {
        // 指数退避: 1s, 2s, 4s, 8s, ... 最大 30s
        return Math.Min(1000 * (int)Math.Pow(2, Math.Min(_reconnectAttempts, 5)), 30000);
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _pollingTimer?.Dispose();
        _webSocket?.Dispose();
        _httpClient.Dispose();
    }

    #endregion
}