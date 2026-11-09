using System.IO;
using StartMode.Shared.Messaging;
using StartMode.Shared.Models;

namespace StudentMessageBall.Services;

/// <summary>
/// 本地缓存服务 - 委托给共享的 MessageStore（去重、限长、未读状态与 JSON 持久化）。
/// </summary>
public class LocalCacheService
{
    private static readonly Lazy<LocalCacheService> _instance = new(() => new LocalCacheService());
    public static LocalCacheService Instance => _instance.Value;

    private readonly MessageStore _store;

    private LocalCacheService()
    {
        var cacheFileName = ConfigService.Instance.Config.Cache.CacheFilePath;
        _store = new MessageStore(
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, cacheFileName),
            ConfigService.Instance.Config.Cache.MaxCachedMessages);
    }

    public IReadOnlyList<StudentMessage> GetMessages() => _store.Snapshot();

    public int UnreadCount => _store.UnreadCount;

    public void AddMessages(IEnumerable<StudentMessage> messages) => _store.AddRange(messages);

    public void MarkAsRead(string messageId) => _store.MarkAsRead(messageId);

    public void MarkAllAsRead() => _store.MarkAllAsRead();

    public void ClearCache() => _store.Clear();

    public StudentMessage? GetLastMessage() => _store.GetLastMessage();
}
