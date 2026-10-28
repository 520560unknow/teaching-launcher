using System.IO;
using System.Text.Json;
using StudentMessageBall.Models;

namespace StudentMessageBall.Services;

/// <summary>
/// 本地缓存服务 - 使用 JSON 文件缓存消息
/// </summary>
public class LocalCacheService
{
    private static readonly Lazy<LocalCacheService> _instance = new(() => new LocalCacheService());
    public static LocalCacheService Instance => _instance.Value;

    private readonly string _cacheFilePath;
    private List<StudentMessage> _cachedMessages = new();
    private readonly object _lock = new();
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private LocalCacheService()
    {
        var cacheFileName = ConfigService.Instance.Config.Cache.CacheFilePath;
        _cacheFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, cacheFileName);
        LoadFromDisk();
    }

    public IReadOnlyList<StudentMessage> GetMessages() { lock (_lock) return _cachedMessages.AsReadOnly(); }

    public int UnreadCount { get { lock (_lock) return _cachedMessages.Count(m => m.Status == MessageStatus.Unread); } }

    public void AddMessages(IEnumerable<StudentMessage> messages)
    {
        lock (_lock)
        {
            var existingIds = new HashSet<string>(_cachedMessages.Select(m => m.Id));
            foreach (var msg in messages)
            {
                if (!existingIds.Contains(msg.Id))
                {
                    _cachedMessages.Add(msg);
                    existingIds.Add(msg.Id);
                }
            }
            TrimCache();
            SaveToDisk();
        }
    }

    public void MarkAsRead(string messageId)
    {
        lock (_lock)
        {
            var msg = _cachedMessages.FirstOrDefault(m => m.Id == messageId);
            if (msg != null)
            {
                msg.Status = MessageStatus.Read;
                SaveToDisk();
            }
        }
    }

    public void MarkAllAsRead()
    {
        lock (_lock)
        {
            foreach (var msg in _cachedMessages.Where(m => m.Status == MessageStatus.Unread))
                msg.Status = MessageStatus.Read;
            SaveToDisk();
        }
    }

    public void ClearCache()
    {
        lock (_lock)
        {
            _cachedMessages.Clear();
            SaveToDisk();
        }
    }

    public StudentMessage? GetLastMessage()
    {
        lock (_lock) return _cachedMessages.OrderByDescending(m => m.Timestamp).FirstOrDefault();
    }

    private void TrimCache()
    {
        var maxCount = ConfigService.Instance.Config.Cache.MaxCachedMessages;
        if (_cachedMessages.Count > maxCount)
        {
            _cachedMessages = _cachedMessages
                .OrderByDescending(m => m.Timestamp)
                .Take(maxCount)
                .ToList();
        }
    }

    private void SaveToDisk()
    {
        try
        {
            var json = JsonSerializer.Serialize(_cachedMessages, _jsonOptions);
            File.WriteAllText(_cacheFilePath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"缓存保存失败: {ex.Message}");
        }
    }

    private void LoadFromDisk()
    {
        try
        {
            if (File.Exists(_cacheFilePath))
            {
                var json = File.ReadAllText(_cacheFilePath);
                var messages = JsonSerializer.Deserialize<List<StudentMessage>>(json, _jsonOptions);
                if (messages != null) _cachedMessages = messages;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"缓存加载失败: {ex.Message}");
            _cachedMessages = new List<StudentMessage>();
        }
    }
}