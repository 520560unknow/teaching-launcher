using System.Windows;
using Microsoft.Web.WebView2.Core;

namespace StartMode.Windows
{
    public partial class WebAppWindow : Window
    {
        private bool _isStarting = false;
        private int _retryCount = 0;
        private const int MaxRetries = 5;

        public WebAppWindow()
        {
            InitializeComponent();
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            await StartAndLoadAsync();
        }

        private async Task StartAndLoadAsync()
        {
            if (_isStarting) return;
            _isStarting = true;
            _retryCount = 0;

            try
            {
                await WebView.EnsureCoreWebView2Async();

                WebView.Visibility = Visibility.Collapsed;
                StatusText.Text = "正在检查课堂互动服务...";
                LoadingProgress.Visibility = Visibility.Visible;

                var ready = await App.JavaAppService.EnsureStartedAsync();

                if (!ready)
                {
                    ShowError("课堂互动系统启动失败。请检查：\n1. JAR 文件是否存在\n2. Java 环境是否正常\n3. 8080 端口是否被其他程序占用");
                    return;
                }

                LoadPage();
            }
            catch (Exception ex)
            {
                ShowError($"启动失败: {ex.Message}");
            }
            finally
            {
                _isStarting = false;
            }
        }

        private async Task EnsureStartedAndLoad()
        {
            WebView.Visibility = Visibility.Collapsed;
            StatusText.Text = "正在检查服务状态...";
            LoadingProgress.Visibility = Visibility.Visible;
            ErrorPanel.Visibility = Visibility.Collapsed;

            var ready = await App.JavaAppService.EnsureStartedAsync();
            if (!ready)
            {
                ShowError("服务启动失败，请检查 Java 环境和 JAR 文件。");
                return;
            }

            LoadPage();
        }

        private void LoadPage()
        {
            var url = App.ConfigService.Config.WebAppUrl;
            StatusText.Text = $"正在加载: {url}";
            LoadingProgress.Visibility = Visibility.Visible;
            ErrorPanel.Visibility = Visibility.Collapsed;
            WebView.Visibility = Visibility.Visible;
            WebView.Source = new Uri(url);
        }

        private void WebView_CoreWebView2InitializationCompleted(object? sender, CoreWebView2InitializationCompletedEventArgs e)
        {
            if (!e.IsSuccess)
            {
                ShowError($"WebView2 初始化失败: {e.InitializationException?.Message}");
                return;
            }

            WebView.CoreWebView2.Settings.IsStatusBarEnabled = false;
            WebView.CoreWebView2.Settings.AreDevToolsEnabled = false;
            WebView.CoreWebView2.NewWindowRequested += WebView_NewWindowRequested;
        }

        private void WebView_NewWindowRequested(object? sender, CoreWebView2NewWindowRequestedEventArgs e)
        {
            e.Handled = true;
            WebView.CoreWebView2.Navigate(e.Uri);
        }

        private void WebView_NavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
        {
            LoadingProgress.Visibility = Visibility.Visible;
            ErrorPanel.Visibility = Visibility.Collapsed;
            WebView.Visibility = Visibility.Visible;
            StatusText.Text = $"正在加载: {e.Uri}";
        }

        private async void WebView_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            LoadingProgress.Visibility = Visibility.Collapsed;

            if (e.IsSuccess)
            {
                StatusText.Text = "加载完成";
                ErrorPanel.Visibility = Visibility.Collapsed;
                _retryCount = 0;
                UpdateNavButtons();
                return;
            }

            var healthy = await App.JavaAppService.IsServerHealthyAsync();

            if (!healthy)
            {
                ShowError("课堂互动服务未运行或已停止。\n请点击「重试」重新启动服务。");
                return;
            }

            if (_retryCount < MaxRetries)
            {
                _retryCount++;
                StatusText.Text = $"页面加载异常，3秒后重试 ({_retryCount}/{MaxRetries})...";
                var timer = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(3)
                };
                timer.Tick += async (s, args) =>
                {
                    timer.Stop();
                    if (WebView?.CoreWebView2 != null)
                    {
                        await EnsureStartedAndLoad();
                    }
                };
                timer.Start();
            }
            else
            {
                ShowError("页面加载失败（可能路径错误或 404）。\n请确认 teacher/dashboard 路径是否正确。");
            }
        }

        private void UpdateNavButtons()
        {
            try
            {
                if (WebView?.CoreWebView2 == null) return;

                BtnBack.IsEnabled = WebView.CoreWebView2.CanGoBack;
                UrlText.Text = WebView.CoreWebView2?.Source?.ToString() ?? "";
            }
            catch { }
        }

        private void BtnBack_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (WebView?.CoreWebView2 != null && WebView.CoreWebView2.CanGoBack)
                {
                    WebView.CoreWebView2.GoBack();
                }
                else
                {
                    BtnHome_Click(sender, e);
                }
            }
            catch
            {
                BtnHome_Click(sender, e);
            }
        }

        private void BtnHome_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var url = App.ConfigService.Config.WebAppUrl;
                if (WebView?.CoreWebView2 != null)
                {
                    WebView.CoreWebView2.Navigate(url);
                }
                else if (WebView != null)
                {
                    WebView.Source = new Uri(url);
                }
            }
            catch { }
        }

        private void ShowError(string message)
        {
            WebView.Visibility = Visibility.Collapsed;
            ErrorPanel.Visibility = Visibility.Visible;
            ErrorText.Text = message;
            StatusText.Text = "加载失败";
            LoadingProgress.Visibility = Visibility.Collapsed;
        }

        private async void RetryButton_Click(object sender, RoutedEventArgs e)
        {
            _retryCount = 0;
            await EnsureStartedAndLoad();
        }

        private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            WebView?.Dispose();
        }
    }
}
