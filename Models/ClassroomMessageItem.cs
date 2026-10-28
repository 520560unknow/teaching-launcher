using System.Text.Json;
using System.Text.Json.Serialization;

namespace StartMode.Models
{
    public enum MessageCategory
    {
        Danmaku,
        Comment,
        Opinion,
        System
    }

    public class ClassroomMessageItem
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
        public MessageCategory Category { get; set; }
        public string Type { get; set; } = "";
        public string ClassroomId { get; set; } = "";
        public string StudentId { get; set; } = "";
        public string StudentName { get; set; } = "";
        public string TeacherName { get; set; } = "";
        public string Title { get; set; } = "";
        public string Content { get; set; } = "";
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public string RawJson { get; set; } = "";

        public static ClassroomMessageItem Parse(string json)
        {
            var item = new ClassroomMessageItem { RawJson = json, Timestamp = DateTime.Now };

            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (root.TryGetProperty("type", out var typeEl))
                    item.Type = typeEl.GetString() ?? "";

                if (root.TryGetProperty("timestamp", out var tsEl))
                {
                    if (tsEl.ValueKind == JsonValueKind.Number && tsEl.TryGetInt64(out var ms))
                        item.Timestamp = DateTimeOffset.FromUnixTimeMilliseconds(ms).LocalDateTime;
                    else if (tsEl.ValueKind == JsonValueKind.String)
                    {
                        if (DateTime.TryParse(tsEl.GetString(), out var dt))
                            item.Timestamp = dt;
                    }
                }

                // 分类
                item.Category = ClassifyType(item.Type);

                // 解析 data 字段
                if (root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Object)
                {
                    // classroomId
                    TryGetString(data, "classroomId", v => item.ClassroomId = v);
                    TryGetString(data, "classroomName", v => { if (string.IsNullOrEmpty(item.ClassroomId)) item.ClassroomId = v; });

                    // student 信息
                    TryGetString(data, "studentId", v => item.StudentId = v);
                    TryGetString(data, "studentName", v => item.StudentName = v);
                    TryGetString(data, "userName", v => { if (string.IsNullOrEmpty(item.StudentName)) item.StudentName = v; });
                    TryGetString(data, "senderName", v => { if (string.IsNullOrEmpty(item.StudentName)) item.StudentName = v; });
                    TryGetString(data, "teacherName", v => item.TeacherName = v);

                    // 内容 - 兼容多种结构
                    TryGetString(data, "content", v => item.Content = v);
                    TryGetString(data, "message", v => { if (string.IsNullOrEmpty(item.Content)) item.Content = v; });
                    TryGetString(data, "text", v => { if (string.IsNullOrEmpty(item.Content)) item.Content = v; });

                    // 嵌套 danmaku / comment / opinion 对象
                    if (data.TryGetProperty("danmaku", out var danmaku) && danmaku.ValueKind == JsonValueKind.Object)
                    {
                        TryGetString(danmaku, "content", v => { if (string.IsNullOrEmpty(item.Content)) item.Content = v; });
                        TryGetString(danmaku, "studentName", v => { if (string.IsNullOrEmpty(item.StudentName)) item.StudentName = v; });
                    }
                    if (data.TryGetProperty("comment", out var comment) && comment.ValueKind == JsonValueKind.Object)
                    {
                        TryGetString(comment, "content", v => { if (string.IsNullOrEmpty(item.Content)) item.Content = v; });
                        TryGetString(comment, "studentName", v => { if (string.IsNullOrEmpty(item.StudentName)) item.StudentName = v; });
                    }
                    if (data.TryGetProperty("opinion", out var opinion) && opinion.ValueKind == JsonValueKind.Object)
                    {
                        TryGetString(opinion, "content", v => { if (string.IsNullOrEmpty(item.Content)) item.Content = v; });
                        TryGetString(opinion, "title", v => item.Title = v);
                        TryGetString(opinion, "studentName", v => { if (string.IsNullOrEmpty(item.StudentName)) item.StudentName = v; });
                    }

                    // 观点标题
                    TryGetString(data, "title", v => item.Title = v);

                    // 系统消息
                    TryGetString(data, "message", v => { if (string.IsNullOrEmpty(item.Content)) item.Content = v; });
                    TryGetString(data, "studentCount", v => { if (string.IsNullOrEmpty(item.Content)) item.Content = $"在线: {v}"; });
                }
            }
            catch
            {
                item.Content = json.Length > 200 ? json[..200] : json;
            }

            return item;
        }

        private static MessageCategory ClassifyType(string type)
        {
            return type?.ToUpperInvariant() switch
            {
                "DANMAKU_RECEIVED" or "STUDENT_DANMAKU" or "TEACHER_DANMAKU_SENT" or "DANMAKU_SENT_SUCCESS" => MessageCategory.Danmaku,
                "COMMENT_RECEIVED" or "NEW_COMMENT" or "TEACHER_COMMENT_SENT" or "COMMENT_SENT_SUCCESS" => MessageCategory.Comment,
                "OPINION_RECEIVED" or "OPINION_SENT_SUCCESS" => MessageCategory.Opinion,
                _ => MessageCategory.System
            };
        }

        private static void TryGetString(JsonElement obj, string prop, Action<string> setter)
        {
            if (obj.TryGetProperty(prop, out var el))
            {
                if (el.ValueKind == JsonValueKind.String)
                    setter(el.GetString() ?? "");
                else if (el.ValueKind != JsonValueKind.Null && el.ValueKind != JsonValueKind.Undefined)
                    setter(el.ToString());
            }
        }

        public string DisplayTime => Timestamp.ToString("HH:mm:ss");

        public string CategoryLabel => Category switch
        {
            MessageCategory.Danmaku => "弹幕",
            MessageCategory.Comment => "评论",
            MessageCategory.Opinion => "观点",
            _ => "系统"
        };

        public string SenderDisplay
        {
            get
            {
                if (!string.IsNullOrEmpty(TeacherName)) return $"教师 {TeacherName}";
                if (!string.IsNullOrEmpty(StudentName)) return StudentName;
                if (!string.IsNullOrEmpty(StudentId)) return StudentId;
                return "未知";
            }
        }
    }
}
