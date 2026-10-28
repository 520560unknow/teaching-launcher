using System.Drawing;
using StudentMessageBall.Services;
using StudentMessageBall.Windows;
using Forms = System.Windows.Forms;
using WpfApp = System.Windows.Application;
using WpfStartup = System.Windows.StartupEventArgs;
using WpfExit = System.Windows.ExitEventArgs;

namespace StudentMessageBall;

/// <summary>
/// 应用程序入口 - 管理托盘图标和窗口生命周期
/// </summary>
public partial class App : WpfApp
{
    private Forms.NotifyIcon? _notifyIcon;
    private FloatingBallWindow? _floatingBallWindow;

    protected override void OnStartup(WpfStartup e)
    {
        base.OnStartup(e);

        // 初始化配置服务（首次加载配置）
        _ = ConfigService.Instance;
    }

    private void Application_Startup(object sender, WpfStartup e)
    {
        // 创建托盘图标
        CreateTrayIcon();

        // 启动时显示悬浮球
        if (ConfigService.Instance.Config.App.ShowBallOnStartup)
            ShowFloatingBall();
    }

    private void Application_Exit(object sender, WpfExit e)
    {
        // 清理资源
        _ = MessageService.Instance.StopAsync();

        // 保存配置
        ConfigService.Instance.Save();

        // 清理托盘图标
        _notifyIcon?.Dispose();
    }

    #region 托盘图标

    private void CreateTrayIcon()
    {
        _notifyIcon = new Forms.NotifyIcon
        {
            Text = "教学消息助手",
            Visible = true
        };

        // 使用系统内置图标，或从资源加载
        try
        {
            var iconPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "app.ico");
            if (System.IO.File.Exists(iconPath))
                _notifyIcon.Icon = new Icon(iconPath);
            else
                _notifyIcon.Icon = System.Drawing.SystemIcons.Application;
        }
        catch
        {
            _notifyIcon.Icon = System.Drawing.SystemIcons.Application;
        }

        // 双击托盘图标显示悬浮球
        _notifyIcon.DoubleClick += (s, e) => ShowFloatingBall();

        // 托盘右键菜单
        var contextMenu = new Forms.ContextMenuStrip();

        var openWeb = new Forms.ToolStripMenuItem("打开教学系统");
        openWeb.Click += (s, e) =>
        {
            ShowFloatingBall();
            _floatingBallWindow?.OpenWebApp();
        };
        contextMenu.Items.Add(openWeb);

        var openMessages = new Forms.ToolStripMenuItem("查看学生消息");
        openMessages.Click += (s, e) => ShowFloatingBall();
        contextMenu.Items.Add(openMessages);

        contextMenu.Items.Add(new Forms.ToolStripSeparator());

        var toggleBall = new Forms.ToolStripMenuItem("显示/隐藏悬浮球");
        toggleBall.Click += (s, e) =>
        {
            if (_floatingBallWindow?.IsVisible == true)
                _floatingBallWindow.Hide();
            else
                ShowFloatingBall();
        };
        contextMenu.Items.Add(toggleBall);

        var settings = new Forms.ToolStripMenuItem("设置");
        settings.Click += (s, e) =>
        {
            var configPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
            if (System.IO.File.Exists(configPath))
                System.Diagnostics.Process.Start("notepad.exe", configPath);
        };
        contextMenu.Items.Add(settings);

        contextMenu.Items.Add(new Forms.ToolStripSeparator());

        var exit = new Forms.ToolStripMenuItem("退出程序");
        exit.Click += (s, e) => Shutdown();
        contextMenu.Items.Add(exit);

        _notifyIcon.ContextMenuStrip = contextMenu;
    }

    #endregion

    #region 悬浮球管理

    private void ShowFloatingBall()
    {
        if (_floatingBallWindow == null)
        {
            _floatingBallWindow = new FloatingBallWindow();
            _floatingBallWindow.Closed += (s, e) => _floatingBallWindow = null;

            // 如果配置了最小化到托盘，关闭时隐藏而不是退出
            if (ConfigService.Instance.Config.App.MinimizeToTray)
            {
                _floatingBallWindow.Closing += (s, e) =>
                {
                    // 阻止关闭，隐藏到托盘
                    if (!IsExiting)
                    {
                        e.Cancel = true;
                        _floatingBallWindow.Hide();
                    }
                };
            }

            _floatingBallWindow.Show();
        }
        else if (!_floatingBallWindow.IsVisible)
        {
            _floatingBallWindow.Show();
        }

        _floatingBallWindow.Activate();
    }

    #endregion

    /// <summary>
    /// 标记程序是否正在完全退出
    /// </summary>
    public static bool IsExiting { get; set; }

    protected override void OnExit(WpfExit e)
    {
        IsExiting = true;
        base.OnExit(e);
    }
}