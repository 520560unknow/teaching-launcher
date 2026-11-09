using System.Text.Json.Serialization;

namespace StartMode.Shared.Models;

/// <summary>
/// 学生消息数据模型（主工程与学生端共用，字段与 JSON 名称保持不变）。
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

    /// <summary>显示用的类型名称</summary>
    [JsonIgnore]
    public string TypeName => Type switch
    {
        MessageType.Comment => "评论",
        MessageType.Danmaku => "弹幕",
        MessageType.Opinion => "观点",
        _ => "未知"
    };

    /// <summary>显示用的时间格式</summary>
    [JsonIgnore]
    public string TimeDisplay => Timestamp.ToString("HH:mm:ss");

    /// <summary>显示用的完整时间</summary>
    [JsonIgnore]
    public string FullTimeDisplay => Timestamp.ToString("yyyy-MM-dd HH:mm:ss");
}

/// <summary>消息类型枚举</summary>
public enum MessageType
{
    Comment,
    Danmaku,
    Opinion
}

/// <summary>消息状态枚举</summary>
public enum MessageStatus
{
    Unread,
    Read,
    Processed
}

/// <summary>消息类型的中文显示名</summary>
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
