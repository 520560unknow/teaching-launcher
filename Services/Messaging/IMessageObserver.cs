using StartMode.Shared.Models;

namespace StartMode.Services.Messaging
{
    /// <summary>
    /// 消息观察者：由消息门面统一推送，UI 只实现这个接口，不再各自拼接事件委托。
    /// 新增关注消息的界面时，实现接口并订阅即可。
    /// </summary>
    public interface IMessageObserver
    {
        void OnMessageReceived(StudentMessage message);

        void OnUnreadCountChanged(int unreadCount);
    }
}
