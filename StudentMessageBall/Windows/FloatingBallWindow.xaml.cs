using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using StudentMessageBall.Services;
using WpfMouseEventArgs = System.Windows.Input.MouseEventArgs;
using WpfMouseButtonEventArgs = System.Windows.Input.MouseButtonEventArgs;
using WpfPoint = System.Windows.Point;
using WpfColor = System.Windows.Media.Color;
using WpfApplication = System.Windows.Application;

namespace StudentMessageBall.Windows;

/// <summary>
/// 透明圆形悬浮球窗口
/// </summary>
public partial class FloatingBallWindow : Window
{
    private bool _isDragging;
    private WpfPoint _dragStartPoint;
    private WebAppWindow? _webAppWindow;
    private MessagePanelWindow? _messagePanelWindow;

    public FloatingBallWindow()
    {
        InitializeComponent();
        Loaded += Window_Loaded;
        Closing += Window_Closing;

        // 订阅消息事件
        MessageService.Instance.OnMessagesReceived += OnMessagesReceived;
        MessageService.Instance.OnConnectionStateChanged += OnConnectionStateChanged;
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        // 设置初始位置
        var config = ConfigService.Instance.Config.FloatingBall;
        var screenWidth = SystemParameters.WorkArea.Width;
        var screenHeight = SystemParameters.WorkArea.Height;

        if (config.DefaultPositionX.HasValue && config.DefaultPositionY.HasValue)
        {
            Left = Math.Max(0, Math.Min(config.DefaultPositionX.Value, screenWidth - Width));
            Top = Math.Max(0, Math.Min(config.DefaultPositionY.Value, screenHeight - Height));
        }
        else
        {
            // 默认右下角
            Left = screenWidth - Width - 20;
            Top = screenHeight - Height - 100;
        }

        Width = config.Size + 16;
        Height = config.Size + 16;

        // 初始化徽标
        UpdateBadge();

        // 启动消息服务
        _ = MessageService.Instance.StartAsync();
    }

    private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        // 保存位置
        var config = ConfigService.Instance.Config.FloatingBall;
        config.DefaultPositionX = Left;
        config.DefaultPositionY = Top;
        ConfigService.Instance.Save();
    }

    #region 拖动逻辑

    private void MainCircle_MouseLeftButtonDown(object sender, WpfMouseButtonEventArgs e)
    {
        _isDragging = false;
        _dragStartPoint = e.GetPosition(this);
        CaptureMouse();
    }

    private void MainCircle_MouseMove(object sender, WpfMouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed) return;

        var currentPoint = e.GetPosition(this);
        var diff = currentPoint - _dragStartPoint;

        if (!_isDragging && (Math.Abs(diff.X) > 3 || Math.Abs(diff.Y) > 3))
            _isDragging = true;

        if (_isDragging)
        {
            Left += diff.X;
            Top += diff.Y;
            // 边界约束
            var screenWidth = SystemParameters.WorkArea.Width;
            var screenHeight = SystemParameters.WorkArea.Height;
            Left = Math.Max(0, Math.Min(Left, screenWidth - Width));
            Top = Math.Max(0, Math.Min(Top, screenHeight - Height));
        }
    }

    private void MainCircle_MouseLeftButtonUp(object sender, WpfMouseButtonEventArgs e)
    {
        ReleaseMouseCapture();
        // 如果没拖动，当作中心点击处理
        if (!_isDragging)
        {
            var pos = e.GetPosition(MainCircle);
            if (IsInCenterArea(pos))
                OpenWebApp();
        }
        _isDragging = false;
    }

    #endregion

    #region 右键菜单

    private void MainCircle_MouseRightButtonDown(object sender, WpfMouseButtonEventArgs e)
    {
        var menu = new System.Windows.Controls.ContextMenu();

        var openWeb = new System.Windows.Controls.MenuItem { Header = "打开教学系统" };
        openWeb.Click += (s, args) => OpenWebApp();
        menu.Items.Add(openWeb);

        var openMessages = new System.Windows.Controls.MenuItem { Header = "查看学生消息" };
        openMessages.Click += (s, args) => ToggleMessagePanel();
        menu.Items.Add(openMessages);

        menu.Items.Add(new System.Windows.Controls.Separator());

        var toggleTop = new System.Windows.Controls.MenuItem
        {
            Header = Topmost ? "取消置顶" : "窗口置顶",
            IsChecked = Topmost
        };
        toggleTop.Click += (s, args) => Topmost = !Topmost;
        menu.Items.Add(toggleTop);

        menu.Items.Add(new System.Windows.Controls.Separator());

        var exit = new System.Windows.Controls.MenuItem { Header = "退出程序" };
        exit.Click += (s, args) => WpfApplication.Current.Shutdown();
        menu.Items.Add(exit);

        menu.IsOpen = true;
        e.Handled = true;
    }

    #endregion

    #region 点击交互

    private void BtnOpenWeb_Click(object sender, RoutedEventArgs e)
    {
        OpenWebApp();
    }

    private void BadgeBorder_MouseLeftButtonDown(object sender, WpfMouseButtonEventArgs e)
    {
        ToggleMessagePanel();
        e.Handled = true;
    }

    private bool IsInCenterArea(WpfPoint pos)
    {
        var centerX = MainCircle.ActualWidth / 2;
        var centerY = MainCircle.ActualHeight / 2;
        var distance = Math.Sqrt(Math.Pow(pos.X - centerX, 2) + Math.Pow(pos.Y - centerY, 2));
        return distance < 22; // 中心 22px 半径区域
    }

    #endregion

    #region 网页窗口管理

    public void OpenWebApp()
    {
        if (_webAppWindow != null && _webAppWindow.IsVisible)
        {
            _webAppWindow.Activate();
            _webAppWindow.WindowState = WindowState.Normal;
            return;
        }

        _webAppWindow = new WebAppWindow();
        _webAppWindow.Closed += (s, e) => _webAppWindow = null;
        _webAppWindow.Show();
        _webAppWindow.Activate();
    }

    #endregion

    #region 消息面板

    private void ToggleMessagePanel()
    {
        if (_messagePanelWindow != null && _messagePanelWindow.IsVisible)
        {
            _messagePanelWindow.Hide();
            return;
        }

        _messagePanelWindow = new MessagePanelWindow();
        _messagePanelWindow.Closed += (s, e) => _messagePanelWindow = null;

        // 定位在悬浮球旁边
        if (Left > SystemParameters.WorkArea.Width / 2)
            _messagePanelWindow.Left = Left - _messagePanelWindow.Width - 10;
        else
            _messagePanelWindow.Left = Left + Width + 10;

        _messagePanelWindow.Top = Math.Max(0, Math.Min(Top, SystemParameters.WorkArea.Height - _messagePanelWindow.Height));
        _messagePanelWindow.Show();
    }

    #endregion

    #region 消息通知

    private void OnMessagesReceived(List<Models.StudentMessage> messages)
    {
        Dispatcher.Invoke(() =>
        {
            UpdateBadge();
            PlayNotificationAnimation();
        });
    }

    private void OnConnectionStateChanged(bool connected)
    {
        Dispatcher.Invoke(() =>
        {
            OfflineIndicator.Visibility = connected ? Visibility.Collapsed : Visibility.Visible;
            MainCircle.Background = new SolidColorBrush(
                connected ? WpfColor.FromRgb(0x4A, 0x90, 0xD9) : WpfColor.FromRgb(0x88, 0x88, 0x88));
        });
    }

    public void UpdateBadge()
    {
        var unreadCount = LocalCacheService.Instance.UnreadCount;
        var maxBadge = ConfigService.Instance.Config.FloatingBall.BadgeMaxCount;

        if (unreadCount > 0)
        {
            BadgeBorder.Visibility = Visibility.Visible;
            TxtBadge.Text = unreadCount > maxBadge ? $"{maxBadge}+" : unreadCount.ToString();
        }
        else
        {
            BadgeBorder.Visibility = Visibility.Collapsed;
        }
    }

    private void PlayNotificationAnimation()
    {
        // 缩放动画
        var scaleAnim = new DoubleAnimation
        {
            From = 1.0,
            To = 1.15,
            Duration = TimeSpan.FromMilliseconds(120),
            AutoReverse = true,
            RepeatBehavior = new RepeatBehavior(2)
        };
        MainCircle.RenderTransform = new ScaleTransform(1, 1);
        MainCircle.RenderTransformOrigin = new WpfPoint(0.5, 0.5);
        MainCircle.RenderTransform.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim);
        MainCircle.RenderTransform.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnim);
    }

    #endregion
}