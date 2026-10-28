using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using StartMode.Models;

using WpfMessageBox = System.Windows.MessageBox;

namespace StartMode.Windows
{
    public partial class FloatingBallWindow : Window
    {
        private bool _isDragging = false;
        private System.Windows.Point _dragStartPoint;
        private WebAppWindow? _webAppWindow;
        private WebSocketMessageWindow? _wsMessageWindow;
        private ServiceControlWindow? _serviceWindow;
        private System.Windows.Forms.NotifyIcon? _notifyIcon;
        private bool _isSubBallsVisible = false;

        public FloatingBallWindow()
        {
            InitializeComponent();
            InitializeTransform();
            InitializeNotifyIcon();
            SetupMessageService();
        }

        private void InitializeTransform()
        {
            MainBall.RenderTransform = new ScaleTransform(1, 1);
            MainBall.RenderTransformOrigin = new System.Windows.Point(0.5, 0.5);
        }

        private void InitializeNotifyIcon()
        {
            try
            {
                _notifyIcon = new System.Windows.Forms.NotifyIcon
                {
                    Icon = System.Drawing.SystemIcons.Application,
                    Visible = true,
                    Text = "课堂互动系统"
                };

                var contextMenu = new System.Windows.Forms.ContextMenuStrip();
                contextMenu.Items.Add("显示/隐藏悬浮球", null, (s, e) => ToggleVisibility());
                contextMenu.Items.Add("退出程序", null, (s, e) => ExitApplication());
                _notifyIcon.ContextMenuStrip = contextMenu;

                _notifyIcon.DoubleClick += (s, e) => ToggleVisibility();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"初始化托盘图标失败: {ex.Message}");
            }
        }

        private void SetupMessageService()
        {
            var messageService = App.MessageService;
            messageService.MessageReceived += OnMessageReceived;
            messageService.UnreadCountChanged += OnUnreadCountChanged;
        }

        private void OnMessageReceived(object? sender, StudentMessage message)
        {
            Dispatcher.Invoke(() =>
            {
                ShowMessageAnimation();
                ShowMessageBubble(message);
            });
        }

        private void OnUnreadCountChanged(object? sender, int count)
        {
            Dispatcher.Invoke(() =>
            {
                UpdateBadge(count);
            });
        }

        private void UpdateBadge(int count)
        {
            if (count > 0)
            {
                MessageBadge.Visibility = Visibility.Visible;
                BadgeCount.Text = count > 99 ? "99+" : count.ToString();
            }
            else
            {
                MessageBadge.Visibility = Visibility.Collapsed;
            }
        }

        private void ShowMessageAnimation()
        {
            var animation = (Storyboard)FindResource("MessageAnimation");
            animation.Begin();
        }

        private void ShowMessageBubble(StudentMessage message)
        {
            BubbleText.Text = $"{message.StudentName}: {message.Content}";
            MessageBubble.Visibility = Visibility.Visible;
            
            var showAnimation = (Storyboard)FindResource("BubbleShowAnimation");
            showAnimation.Begin();

            var timer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(3)
            };
            timer.Tick += (s, e) =>
            {
                timer.Stop();
                var hideAnimation = (Storyboard)FindResource("BubbleHideAnimation");
                hideAnimation.Completed += (s2, e2) => MessageBubble.Visibility = Visibility.Collapsed;
                hideAnimation.Begin();
            };
            timer.Start();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                var screenWidth = SystemParameters.PrimaryScreenWidth;
                var screenHeight = SystemParameters.PrimaryScreenHeight;
                
                Left = screenWidth - Width - 50;
                Top = screenHeight - Height - 150;
                
                Visibility = Visibility.Visible;
                Topmost = true;
                
                System.Diagnostics.Debug.WriteLine($"悬浮球已加载，位置: ({Left}, {Top})");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"加载悬浮球失败: {ex.Message}");
            }
        }

        private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            _notifyIcon?.Dispose();
        }

        #region 拖动功能

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            if (!_isDragging)
            {
                _isDragging = true;
                _dragStartPoint = e.GetPosition(this);
                CaptureMouse();
            }
            base.OnMouseLeftButtonDown(e);
        }

        protected override void OnMouseMove(System.Windows.Input.MouseEventArgs e)
        {
            if (_isDragging)
            {
                var currentPosition = e.GetPosition(this);
                var offset = currentPosition - _dragStartPoint;
                
                Left += offset.X;
                Top += offset.Y;
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            if (_isDragging)
            {
                _isDragging = false;
                ReleaseMouseCapture();
            }
            base.OnMouseLeftButtonUp(e);
        }

        #endregion

        #region 点击事件

        private void MainBall_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            ToggleSubBalls();
        }

        private void MainBall_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            BallContextMenu.IsOpen = true;
        }

        private void MessageBadge_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            OpenMessagePanel();
        }

        private void SubBall1_Click(object sender, RoutedEventArgs e)
        {
            HideSubBalls();
            OpenWebApp();
        }

        private void SubBall2_Click(object sender, RoutedEventArgs e)
        {
            HideSubBalls();
            OpenMessagePanel();
        }

        private void SubBall3_Click(object sender, RoutedEventArgs e)
        {
            HideSubBalls();
            OpenServiceControl();
        }

        #endregion

        #region 菜单操作

        private void ToggleVisibility_Click(object sender, RoutedEventArgs e)
        {
            ToggleVisibility();
        }

        private void ExitApp_Click(object sender, RoutedEventArgs e)
        {
            ExitApplication();
        }

        private void ServiceControl_Click(object sender, RoutedEventArgs e)
        {
            OpenServiceControl();
        }

        #endregion

        #region 子悬浮球控制

        private void ToggleSubBalls()
        {
            if (_isSubBallsVisible)
            {
                HideSubBalls();
            }
            else
            {
                ShowSubBalls();
            }
        }

        private void ShowSubBalls()
        {
            SubBallsPanel.Visibility = Visibility.Visible;
            _isSubBallsVisible = true;
        }

        private void HideSubBalls()
        {
            SubBallsPanel.Visibility = Visibility.Collapsed;
            _isSubBallsVisible = false;
        }

        #endregion

        #region 功能方法

        /// <summary>
        /// 更新托盘提示文字，让用户知道后台服务正在启动还是已就绪。
        /// </summary>
        public void UpdateStartupStatus(string text)
        {
            Dispatcher.Invoke(() =>
            {
                if (_notifyIcon != null)
                {
                    _notifyIcon.Text = text.Length > 63 ? text.Substring(0, 63) : text;
                }
            });
        }

        private void OpenWebApp()
        {
            try
            {
                if (_webAppWindow == null || !_webAppWindow.IsLoaded)
                {
                    _webAppWindow = new WebAppWindow();
                    _webAppWindow.Show();
                }
                else
                {
                    _webAppWindow.WindowState = WindowState.Normal;
                    _webAppWindow.Activate();
                }
            }
            catch (InvalidOperationException)
            {
                if (_webAppWindow != null)
                {
                    _webAppWindow.WindowState = WindowState.Normal;
                    _webAppWindow.Activate();
                }
            }
        }

        private void OpenMessagePanel()
        {
            if (_wsMessageWindow == null || !_wsMessageWindow.IsLoaded)
            {
                _wsMessageWindow = new WebSocketMessageWindow();

                var screenLeft = SystemParameters.VirtualScreenLeft;
                var screenWidth = SystemParameters.VirtualScreenWidth;

                if (Left + Width + 430 < screenLeft + screenWidth)
                {
                    _wsMessageWindow.Left = Left + Width + 10;
                }
                else
                {
                    _wsMessageWindow.Left = Left - 440;
                }
                _wsMessageWindow.Top = Top;

                _wsMessageWindow.Show();
                _wsMessageWindow.Activate();
            }
            else
            {
                if (_wsMessageWindow.Visibility == Visibility.Visible)
                {
                    _wsMessageWindow.Hide();
                }
                else
                {
                    _wsMessageWindow.Show();
                    _wsMessageWindow.Activate();
                }
            }
        }

        private void OpenServiceControl()
        {
            if (_serviceWindow == null || !_serviceWindow.IsLoaded)
            {
                _serviceWindow = new ServiceControlWindow();

                var screenLeft = SystemParameters.VirtualScreenLeft;
                var screenWidth = SystemParameters.VirtualScreenWidth;

                if (Left + Width + 360 < screenLeft + screenWidth)
                    _serviceWindow.Left = Left + Width + 10;
                else
                    _serviceWindow.Left = Math.Max(screenLeft + 10, Left - 360);

                _serviceWindow.Top = Math.Max(SystemParameters.VirtualScreenTop + 10, Top);
                _serviceWindow.Show();
                _serviceWindow.Activate();
            }
            else
            {
                if (_serviceWindow.Visibility == Visibility.Visible)
                    _serviceWindow.Hide();
                else
                {
                    _serviceWindow.Show();
                    _serviceWindow.Activate();
                }
            }
        }

        private void ToggleVisibility()
        {
            if (Visibility == Visibility.Visible)
            {
                Visibility = Visibility.Collapsed;
            }
            else
            {
                Visibility = Visibility.Visible;
            }
        }

        private void ExitApplication()
        {
            var result = WpfMessageBox.Show("确定要退出程序吗？", "退出确认", 
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            
            if (result == MessageBoxResult.Yes)
            {
                System.Windows.Application.Current.Shutdown();
            }
        }

        #endregion
    }
}
