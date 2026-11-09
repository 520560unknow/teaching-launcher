namespace StartMode.Shared.Protocol;

/// <summary>
/// 课堂 WebSocket 消息类型常量（与后端 /ws 消息字段保持一致，不新增、不改名）。
/// </summary>
public static class WsMessageTypes
{
    public const string DanmakuReceived = "DANMAKU_RECEIVED";
    public const string StudentDanmaku = "STUDENT_DANMAKU";
    public const string TeacherDanmakuSent = "TEACHER_DANMAKU_SENT";
    public const string DanmakuSentSuccess = "DANMAKU_SENT_SUCCESS";

    public const string CommentReceived = "COMMENT_RECEIVED";
    public const string NewComment = "NEW_COMMENT";
    public const string TeacherCommentSent = "TEACHER_COMMENT_SENT";
    public const string CommentSentSuccess = "COMMENT_SENT_SUCCESS";

    public const string OpinionReceived = "OPINION_RECEIVED";
    public const string OpinionSentSuccess = "OPINION_SENT_SUCCESS";

    public const string RequestStudents = "REQUEST_STUDENTS";
    public const string OnlineStudents = "ONLINE_STUDENTS";
    public const string ToggleStudentDanmaku = "TOGGLE_STUDENT_DANMAKU";
    public const string Heartbeat = "HEARTBEAT";
    public const string Ping = "PING";
}

/// <summary>
/// 课堂 WebSocket 地址构造与固定负载。
/// </summary>
public static class WsEndpoints
{
    public const string PingPayload = "{\"type\":\"PING\"}";

    public static string StudentUri(string baseUrl, string classroomId, string studentId)
        => $"{baseUrl.TrimEnd('/')}/ws/student/{Uri.EscapeDataString(classroomId)}?studentId={Uri.EscapeDataString(studentId)}";

    public static string TeacherUri(string baseUrl, string classroomId)
        => $"{baseUrl.TrimEnd('/')}/ws/teacher/{Uri.EscapeDataString(classroomId)}";
}
