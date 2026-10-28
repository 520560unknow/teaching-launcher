using System.Text.Json.Serialization;

namespace StartMode.Models
{
    public enum MessageType
    {
        Comment,
        Danmaku,
        Opinion
    }

    public enum MessageStatus
    {
        Unread,
        Read,
        Processed
    }

    public class StudentMessage
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();

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

        // 显示用的类型名称
        [JsonIgnore]
        public string TypeName => Type switch
        {
            MessageType.Comment => "评论",
            MessageType.Danmaku => "弹幕",
            MessageType.Opinion => "观点",
            _ => "未知"
        };

        // 显示用的时间格式
        [JsonIgnore]
        public string TimeDisplay => Timestamp.ToString("HH:mm:ss");

        // 显示用的完整时间
        [JsonIgnore]
        public string FullTimeDisplay => Timestamp.ToString("yyyy-MM-dd HH:mm:ss");
    }
}
