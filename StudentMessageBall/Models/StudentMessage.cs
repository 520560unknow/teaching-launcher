using System.Text.Json.Serialization;

namespace StudentMessageBall.Models;

/// <summary>
/// 学生消息数据模型
/// </summary>
public class StudentMessage
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    [JsonPropertyName("studentId")]
    public string StudentId { get; set; } = string.Empty;

    [JsonPropertyName("studentName")]
    public string StudentName { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public MessageType Type { get; set; } = MessageType.Comment;

    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.Now;

    [JsonPropertyName("source")]
    public string Source { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public MessageStatus Status { get; set; } = MessageStatus.Unread;
}

/// <summary>
/// 消息类型枚举
/// </summary>
public enum MessageType
{
    [JsonPropertyName("comment")]
    Comment,

    [JsonPropertyName("danmaku")]
    Danmaku,

    [JsonPropertyName("opinion")]
    Opinion
}

/// <summary>
/// 消息状态枚举
/// </summary>
public enum MessageStatus
{
    [JsonPropertyName("unread")]
    Unread,

    [JsonPropertyName("read")]
    Read,

    [JsonPropertyName("processed")]
    Processed
}

/// <summary>
/// 消息类型的中文显示名
/// </summary>
public static class MessageTypeExtensions
{
    public static string ToChineseName(this MessageType type) => type switch
    {
        MessageType.Comment => "评论",
        MessageType.Danmaku => "弹幕",
        MessageType.Opinion => "观点",
        _ => "未知"
    };
}