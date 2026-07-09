using System.Windows;
using System.Windows.Input;

namespace CodexAppSwitcher;

/// <summary>
/// 当前账号额度桌面悬浮挂件。
/// </summary>
public partial class UsageWidgetWindow : Window
{
    private bool _hasPosition;

    /// <summary>
    /// 初始化额度挂件窗口。
    /// </summary>
    public UsageWidgetWindow()
    {
        InitializeComponent();
    }

    private void Window_OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_hasPosition)
        {
            return;
        }

        var area = SystemParameters.WorkArea;
        Left = area.Right - Width - 24;
        Top = area.Top + 72;
        _hasPosition = true;
    }

    private void Window_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount >= 2)
        {
            ShowMainWindow();
            e.Handled = true;
            return;
        }

        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private static void ShowMainWindow()
    {
        var mainWindow = Application.Current.MainWindow;
        if (mainWindow is null)
        {
            return;
        }

        if (mainWindow.WindowState == WindowState.Minimized)
        {
            mainWindow.WindowState = WindowState.Normal;
        }

        mainWindow.Show();
        mainWindow.Activate();
    }
}
