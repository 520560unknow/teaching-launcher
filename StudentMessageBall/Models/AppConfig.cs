using System.Text.Json.Serialization;

namespace StudentMessageBall.Models;

/// <summary>
/// 应用程序配置模型
/// </summary>
public class AppConfig
{
    [JsonPropertyName("WebApp")]
    public WebAppConfig WebApp { get; set; } = new();

    [JsonPropertyName("MessageService")]
    public MessageServiceConfig MessageService { get; set; } = new();

    [JsonPropertyName("FloatingBall")]
    public FloatingBallConfig FloatingBall { get; set; } = new();

    [JsonPropertyName("Cache")]
    public CacheConfig Cache { get; set; } = new();

    [JsonPropertyName("App")]
    public AppSettingsConfig App { get; set; } = new();
}

public class WebAppConfig
{
    [JsonPropertyName("Url")]
    public string Url { get; set; } = "https://example.com";

    [JsonPropertyName("WindowTitle")]
    public string WindowTitle { get; set; } = "教学系统";

    [JsonPropertyName("WindowWidth")]
    public int WindowWidth { get; set; } = 1200;

    [JsonPropertyName("WindowHeight")]
    public int WindowHeight { get; set; } = 800;
}

public class MessageServiceConfig
{
    [JsonPropertyName("Mode")]
    public string Mode { get; set; } = "Polling";

    [JsonPropertyName("WebSocketUrl")]
    public string WebSocketUrl { get; set; } = "wss://example.com/ws/messages";

    [JsonPropertyName("PollingIntervalSeconds")]
    public int PollingIntervalSeconds { get; set; } = 5;

    [JsonPropertyName("ApiBaseUrl")]
    public string ApiBaseUrl { get; set; } = "https://example.com/api";

    [JsonPropertyName("MessagesEndpoint")]
    public string MessagesEndpoint { get; set; } = "/messages";

    [JsonPropertyName("AutoReconnect")]
    public bool AutoReconnect { get; set; } = true;

    [JsonPropertyName("MaxReconnectAttempts")]
    public int MaxReconnectAttempts { get; set; } = 10;
}

public class FloatingBallConfig
{
    [JsonPropertyName("Size")]
    public int Size { get; set; } = 80;

    [JsonPropertyName("DefaultPositionX")]
    public double? DefaultPositionX { get; set; }

    [JsonPropertyName("DefaultPositionY")]
    public double? DefaultPositionY { get; set; }

    [JsonPropertyName("AlwaysOnTop")]
    public bool AlwaysOnTop { get; set; } = true;

    [JsonPropertyName("BadgeMaxCount")]
    public int BadgeMaxCount { get; set; } = 99;
}

public class CacheConfig
{
    [JsonPropertyName("MaxCachedMessages")]
    public int MaxCachedMessages { get; set; } = 1000;

    [JsonPropertyName("CacheFilePath")]
    public string CacheFilePath { get; set; } = "cache_messages.json";
}

public class AppSettingsConfig
{
    [JsonPropertyName("AutoStart")]
    public bool AutoStart { get; set; } = false;

    [JsonPropertyName("MinimizeToTray")]
    public bool MinimizeToTray { get; set; } = true;

    [JsonPropertyName("ShowBallOnStartup")]
    public bool ShowBallOnStartup { get; set; } = true;
}