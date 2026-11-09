using System.Text.Json;
using StartMode.Shared.Models;

namespace StartMode.Shared.Messaging;

/// <summary>
/// 消息存储：按 id 去重、限制最大缓存、维护未读状态，并持久化为 JSON 文件。
/// 主工程与学生端共用，缓存文件路径与 JSON 结构保持不变。
/// </summary>
public sealed class MessageStore
{
    private readonly object _lock = new();
    private readonly string _filePath;
    private readonly int _maxCachedMessages;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private List<StudentMessage> _messages;

    public MessageStore(string filePath, int maxCachedMessages = 1000)
    {
        _filePath = filePath;
        _maxCachedMessages = maxCachedMessages > 0 ? maxCachedMessages : 1000;
        _messages = Load();
    }

    public int UnreadCount
    {
        get { lock (_lock) return _messages.Count(m => m.Status == MessageStatus.Unread); }
    }

    public IReadOnlyList<StudentMessage> Snapshot()
    {
        lock (_lock) return _messages.ToList();
    }

    public void Add(StudentMessage message) => AddRange(new[] { message });

    public void AddRange(IEnumerable<StudentMessage> messages)
    {
        lock (_lock)
        {
            var existingIds = new HashSet<string>(_messages.Select(m => m.Id));
            foreach (var message in messages)
            {
                if (existingIds.Add(message.Id))
                {
                    _messages.Add(message);
                }
            }
            Trim();
            SaveLocked();
        }
    }

    public bool MarkAsRead(string messageId)
    {
        lock (_lock)
        {
            var message = _messages.FirstOrDefault(m => m.Id == messageId);
            if (message == null)
            {
                return false;
            }
            message.Status = MessageStatus.Read;
            SaveLocked();
            return true;
        }
    }

    public void MarkAllAsRead()
    {
        lock (_lock)
        {
            foreach (var message in _messages.Where(m => m.Status == MessageStatus.Unread))
            {
                message.Status = MessageStatus.Read;
            }
            SaveLocked();
        }
    }

    public StudentMessage? GetLastMessage()
    {
        lock (_lock) return _messages.OrderByDescending(m => m.Timestamp).FirstOrDefault();
    }

    public void Clear()
    {
        lock (_lock)
        {
            _messages.Clear();
            SaveLocked();
        }
    }

    public void Save()
    {
        lock (_lock) SaveLocked();
    }

    private void Trim()
    {
        if (_messages.Count <= _maxCachedMessages)
        {
            return;
        }
        _messages = _messages
            .OrderByDescending(m => m.Timestamp)
            .Take(_maxCachedMessages)
            .ToList();
    }

    private void SaveLocked()
    {
        try
        {
            var json = JsonSerializer.Serialize(_messages, _jsonOptions);
            File.WriteAllText(_filePath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"保存缓存消息失败: {ex.Message}");
        }
    }

    private List<StudentMessage> Load()
    {
        try
        {
            if (File.Exists(_filePath))
            {
                var json = File.ReadAllText(_filePath);
                var messages = JsonSerializer.Deserialize<List<StudentMessage>>(json, _jsonOptions);
                if (messages != null)
                {
                    return messages;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"加载缓存消息失败: {ex.Message}");
        }
        return new List<StudentMessage>();
    }
}
