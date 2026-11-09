using System.Windows;
using StartMode.Services;
using StartMode.Services.Messaging;

namespace StartMode
{
    public partial class App : System.Windows.Application
    {
        private IMessageService? _messageService;
        private ConfigService? _configService;
        private IJavaAppService? _javaAppService;
        private ILlamaService? _llamaService;
        private IWebSocketMonitorService? _wsMonitorService;
        private Windows.FloatingBallWindow? _floatingBallWindow;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            
            try
            {
                // 初始化配置服务
                var configService = new ConfigService();
                _configService = configService;
                var config = configService.Config;

                // 初始化 Java 应用服务、本地模型服务与课堂 WebSocket 监听
                var javaAppService = new JavaAppService(config);
                _javaAppService = javaAppService;
                var llamaService = new LlamaService(config);
                _llamaService = llamaService;
                var wsMonitorService = new WebSocketMonitorService(config);
                _wsMonitorService = wsMonitorService;

                // 初始化消息服务：来源由工厂组装，统一交给 MessageStore
                var messageService = new MessageService(config, MessageSourceFactory.Create(config, wsMonitorService));
                _messageService = messageService;
                messageService.StartListening();

                // 创建并显示悬浮球窗口
                _floatingBallWindow = new Windows.FloatingBallWindow(
                    config, messageService, javaAppService, llamaService, wsMonitorService);
                MainWindow = _floatingBallWindow;
                _floatingBallWindow.Show();
                _floatingBallWindow.Activate();

                // 后台预热 Spring Boot，用户看到悬浮球时服务已在启动
                var javaWarmup = Task.Run(async () =>
                {
                    try
                    {
                        System.Diagnostics.Debug.WriteLine("[App] 后台预热 Spring Boot...");
                        var ready = await _javaAppService.EnsureStartedAsync();
                        System.Diagnostics.Debug.WriteLine($"[App] Spring Boot 预热结果: {(ready ? "就绪" : "失败")}");
                        return ready;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[App] 预热异常: {ex.Message}");
                        return false;
                    }
                });

                // 后台预热本地模型：学生端检索/答疑直接可用，教师无需手动启动
                var aiWarmup = Task.Run(async () =>
                {
                    try
                    {
                        System.Diagnostics.Debug.WriteLine("[App] 后台预热 AI 服务...");
                        var (_, message) = await _llamaService.StartAsync();
                        System.Diagnostics.Debug.WriteLine($"[App] AI 预热结果: {message}");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[App] AI 预热异常: {ex.Message}");
                    }
                });

                // 预热完成后更新托盘提示，避免用户误以为程序没反应
                _floatingBallWindow.UpdateStartupStatus("课堂互动系统 · 正在启动服务…");
                _ = Task.Run(async () =>
                {
                    var javaReady = await javaWarmup;
                    await aiWarmup;
                    _floatingBallWindow.UpdateStartupStatus(javaReady
                        ? "课堂互动系统 · 服务已就绪"
                        : "课堂互动系统 · 网页服务未就绪，请打开服务控制检查");
                });

                System.Diagnostics.Debug.WriteLine("应用程序启动成功");
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"启动失败: {ex.Message}\n\n{ex.StackTrace}", "错误", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            try
            {
                var stopTask = _llamaService?.StopAsync();
                if (stopTask != null && !stopTask.Wait(TimeSpan.FromSeconds(2)))
                {
                    _llamaService!.StopImmediately();
                    System.Diagnostics.Debug.WriteLine("AI 服务停止超时，已强制结束");
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"停止 AI 服务失败: {ex.Message}"); }

            try { _javaAppService?.StopJavaApp(); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"停止 Java 失败: {ex.Message}"); }

            try { _wsMonitorService?.Dispose(); }
            catch { }

            try { _messageService?.StopListening(); }
            catch { }

            try { _configService?.SaveConfig(); }
            catch { }

            base.OnExit(e);
        }
    }
}
