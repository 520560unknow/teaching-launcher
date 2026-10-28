using System.Windows;
using StartMode.Services;

namespace StartMode
{
    public partial class App : System.Windows.Application
    {
        private static MessageService? _messageService;
        private static ConfigService? _configService;
        private static JavaAppService? _javaAppService;
        private static LlamaService? _llamaService;
        private static WebSocketMonitorService? _wsMonitorService;
        private static Windows.FloatingBallWindow? _floatingBallWindow;

        public static MessageService MessageService => _messageService ??= new MessageService();
        public static ConfigService ConfigService => _configService ??= new ConfigService();
        public static JavaAppService JavaAppService => _javaAppService ??= new JavaAppService();
        public static LlamaService LlamaService => _llamaService ??= new LlamaService();
        public static WebSocketMonitorService WsMonitorService => _wsMonitorService ??= new WebSocketMonitorService();

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            
            try
            {
                // 初始化配置服务
                _configService = new ConfigService();
                
                // 初始化消息服务
                _messageService = new MessageService();
                _messageService.StartListening();
                
                // 初始化 Java 应用服务
                _javaAppService = new JavaAppService();
                _llamaService = new LlamaService();
                
                // 创建并显示悬浮球窗口
                _floatingBallWindow = new Windows.FloatingBallWindow();
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
            try { _llamaService?.StopAsync().Wait(TimeSpan.FromSeconds(8)); }
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
