using StartMode.Models;

namespace StartMode.Services.Messaging
{
    /// <summary>
    /// 消息来源工厂：集中决定"一个运行实例拥有哪些消息来源"，避免在组合根里散落 new 表达式。
    /// 新增来源（例如以后的系统通知）只需要改这里。
    /// </summary>
    public static class MessageSourceFactory
    {
        public static IMessageSource[] Create(AppConfig config, IWebSocketMonitorService monitor)
        {
            return new IMessageSource[]
            {
                new WebSocketSource(monitor),
                new SimulatedMessageSource(config)
            };
        }
    }
}
