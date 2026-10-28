using System.Windows;
using Microsoft.Web.WebView2.Core;
using StudentMessageBall.Services;
using WpfMessageBox = System.Windows.MessageBox;

namespace StudentMessageBall.Windows;

/// <summary>
/// 内嵌 WebView2 的网页主窗口
/// </summary>
public partial class WebAppWindow : Window
{
    private readonly string _targetUrl;

    public WebAppWindow()
    {
        InitializeComponent();
        _targetUrl = ConfigService.Instance.Config.WebApp.Url;
        Title = ConfigService.Instance.Config.WebApp.WindowTitle;

        Width = ConfigService.Instance.Config.WebApp.WindowWidth;
        Height = ConfigService.Instance.Config.WebApp.WindowHeight;

        Loaded += async (s, e) => await InitializeWebViewAsync();
    }

    private async Task InitializeWebViewAsync()
    {
        try
        {
            TxtStatus.Text = "正在初始化 WebView2...";

            var env = await CoreWebView2Environment.CreateAsync();
            await WebView.EnsureCoreWebView2Async(env);

            WebView.CoreWebView2.Settings.IsStatusBarEnabled = false;
            WebView.CoreWebView2.Settings.AreDefaultScriptDialogsEnabled = true;
            WebView.CoreWebView2.Settings.IsWebMessageEnabled = true;

            // 监听网页 postMessage
            WebView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;

            TxtStatus.Text = "正在加载页面...";
            WebView.CoreWebView2.Navigate(_targetUrl);
        }
        catch (Exception ex)
        {
            ShowError("WebView2 初始化失败", ex.Message);
        }
    }

    private void WebView_NavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        TxtStatus.Text = $"正在加载: {e.Uri}";
        TxtUrl.Text = e.Uri;
        ErrorOverlay.Visibility = Visibility.Collapsed;
    }

    private void WebView_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        if (e.IsSuccess)
        {
            TxtStatus.Text = "加载完成";
            ErrorOverlay.Visibility = Visibility.Collapsed;
        }
        else
        {
            var errorMsg = e.WebErrorStatus switch
            {
                CoreWebView2WebErrorStatus.ConnectionAborted => "连接被中止",
                CoreWebView2WebErrorStatus.ConnectionReset => "连接重置",
                CoreWebView2WebErrorStatus.HostNameNotResolved => "无法解析服务器地址",
                CoreWebView2WebErrorStatus.Timeout => "连接超时",
                CoreWebView2WebErrorStatus.ServerUnreachable => "服务器不可达",
                CoreWebView2WebErrorStatus.Unknown => "未知错误",
                _ => $"错误: {e.WebErrorStatus}"
            };
            ShowError("页面加载失败", errorMsg);
        }
    }

    private void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        // 接收网页端通过 postMessage 发送的学生消息
        Dispatcher.Invoke(() =>
        {
            try
            {
                var json = e.TryGetWebMessageAsString();
                if (!string.IsNullOrEmpty(json))
                {
                    System.Diagnostics.Debug.WriteLine($"收到网页消息: {json}");
                }
            }
            catch { }
        });
    }

    private void ShowError(string title, string detail)
    {
        TxtError.Text = title;
        TxtErrorDetail.Text = detail;
        ErrorOverlay.Visibility = Visibility.Visible;
        TxtStatus.Text = "加载失败";
    }

    private void BtnBack_Click(object sender, RoutedEventArgs e)
    {
        if (WebView.CoreWebView2?.CanGoBack == true)
            WebView.CoreWebView2.GoBack();
    }

    private void BtnForward_Click(object sender, RoutedEventArgs e)
    {
        if (WebView.CoreWebView2?.CanGoForward == true)
            WebView.CoreWebView2.GoForward();
    }

    private void BtnRefresh_Click(object sender, RoutedEventArgs e)
    {
        WebView.CoreWebView2?.Reload();
    }

    private void BtnSettings_Click(object sender, RoutedEventArgs e)
    {
        WpfMessageBox.Show(
            $"当前系统地址: {_targetUrl}\n\n要修改系统地址，请编辑程序目录下的 appsettings.json 文件。",
            "设置", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void BtnRetry_Click(object sender, RoutedEventArgs e)
    {
        ErrorOverlay.Visibility = Visibility.Collapsed;
        WebView.CoreWebView2?.Reload();
    }

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        // 不阻止关闭，由托盘机制管理
    }
}