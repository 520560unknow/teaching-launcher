using StartMode.Shared.Models;

namespace StartMode.Services.Messaging
{
    /// <summary>
    /// 消息来源：只负责把消息推出来，缓存与未读状态统一交给 MessageStore。
    /// </summary>
    public interface IMessageSource
    {
        event EventHandler<StudentMessage>? MessageProduced;

        void Start();

        void Stop();
    }
}
