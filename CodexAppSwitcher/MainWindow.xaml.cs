using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using CodexAppSwitcher.ViewModels;

namespace CodexAppSwitcher;

/// <summary>
/// CodexAppSwitcher 主窗口。
/// </summary>
public partial class MainWindow : Window
{
    private bool operationLogScrollHooked;

    /// <summary>
    /// 初始化主窗口。
    /// </summary>
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainWindowViewModel();
    }

    private void Window_OnLoaded(object sender, RoutedEventArgs e)
    {
        if (operationLogScrollHooked || DataContext is not MainWindowViewModel viewModel)
        {
            ScrollOperationLogsToEndAsync();
            return;
        }

        viewModel.OperationLogs.CollectionChanged += (_, _) => ScrollOperationLogsToEndAsync();
        operationLogScrollHooked = true;
        ScrollOperationLogsToEndAsync();
    }

    private void TitleBar_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            ToggleWindowState();
            return;
        }

        DragMove();
    }

    private void MinimizeButton_OnClick(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void MaximizeButton_OnClick(object sender, RoutedEventArgs e)
    {
        ToggleWindowState();
    }

    private void CloseButton_OnClick(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void ToggleWindowState()
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    private void ScrollOperationLogsToEndAsync()
    {
        Dispatcher.BeginInvoke(
            () => OperationLogScrollViewer.ScrollToEnd(),
            DispatcherPriority.Background);
    }
}
