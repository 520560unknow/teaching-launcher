using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using MediaColor = System.Windows.Media.Color;

namespace StartMode.Windows
{
    public partial class ServiceControlWindow : Window
    {
        private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(3) };
        private readonly int _webPort;
        private bool _busy;

        public ServiceControlWindow()
        {
            InitializeComponent();

            _webPort = Uri.TryCreate(App.ConfigService.Config.ServerBaseUrl, UriKind.Absolute, out var uri)
                ? uri.Port
                : 8080;

            _timer.Tick += async (_, _) => await RefreshAsync();
        }

        protected override void OnClosed(EventArgs e)
        {
            _timer.Stop();
            base.OnClosed(e);
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            await RefreshAsync();
            _timer.Start();
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
                DragMove();
        }

        private async Task RefreshAsync()
        {
            if (_busy)
                return;

            var webRunning = await App.JavaAppService.IsServerHealthyAsync();
            var aiRunning = await App.LlamaService.IsRunningAsync();

            ApplyState(WebStatusDot, WebStatusText, webRunning, App.JavaAppService.IsStarting,
                $"运行中 · 端口 {_webPort}", "已停止");
            ApplyState(AiStatusDot, AiStatusText, aiRunning, App.LlamaService.IsStarting,
                $"运行中 · 端口 {App.LlamaService.Port}", "已停止");
        }

        private static void ApplyState(Ellipse dot, System.Windows.Controls.TextBlock text, bool running,
            bool starting, string runningText, string stoppedText)
        {
            if (running)
            {
                dot.Fill = new SolidColorBrush(MediaColor.FromRgb(0x4C, 0xAF, 0x50));
                text.Text = runningText;
            }
            else if (starting)
            {
                // 启动中：黄色，避免用户误以为服务没起来
                dot.Fill = new SolidColorBrush(MediaColor.FromRgb(0xFF, 0xB7, 0x4D));
                text.Text = "启动中…（首次加载较慢）";
            }
            else
            {
                dot.Fill = new SolidColorBrush(MediaColor.FromRgb(0x77, 0x77, 0x77));
                text.Text = stoppedText;
            }
        }

        private async void WebStart_Click(object sender, RoutedEventArgs e)
        {
            if (!BeginBusy("正在启动网页服务…"))
                return;

            var ok = await App.JavaAppService.StartAsync();
            EndBusy(ok ? "网页服务已启动" : "网页服务启动失败，请检查 Java 与 JAR 路径");
            await RefreshAsync();
        }

        private async void WebStop_Click(object sender, RoutedEventArgs e)
        {
            if (!BeginBusy("正在关闭网页服务…"))
                return;

            var (ok, message) = await App.JavaAppService.StopAsync();
            EndBusy(message, ok);
            await RefreshAsync();
        }

        private async void AiStart_Click(object sender, RoutedEventArgs e)
        {
            if (!BeginBusy("正在启动 AI 服务（首次加载模型较慢）…"))
                return;

            var (ok, message) = await App.LlamaService.StartAsync();
            EndBusy(message, ok);
            await RefreshAsync();
        }

        private async void AiStop_Click(object sender, RoutedEventArgs e)
        {
            if (!BeginBusy("正在关闭 AI 服务…"))
                return;

            var (ok, message) = await App.LlamaService.StopAsync();
            EndBusy(message, ok);
            await RefreshAsync();
        }

        private bool BeginBusy(string hint)
        {
            if (_busy)
                return false;

            _busy = true;
            BtnWebStart.IsEnabled = false;
            BtnWebStop.IsEnabled = false;
            BtnAiStart.IsEnabled = false;
            BtnAiStop.IsEnabled = false;
            HintText.Foreground = new SolidColorBrush(MediaColor.FromRgb(0xBB, 0xBB, 0xBB));
            HintText.Text = hint;
            return true;
        }

        private void EndBusy(string message, bool success = true)
        {
            _busy = false;
            BtnWebStart.IsEnabled = true;
            BtnWebStop.IsEnabled = true;
            BtnAiStart.IsEnabled = true;
            BtnAiStop.IsEnabled = true;
            HintText.Foreground = new SolidColorBrush(success
                ? MediaColor.FromRgb(0x66, 0x88, 0x99)
                : MediaColor.FromRgb(0xFF, 0x8A, 0x80));
            HintText.Text = message;
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
