using System;
using System.Windows;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Threading;
using CodexAppSwitcher.ViewModels;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace CodexAppSwitcher;

/// <summary>
/// CodexAppSwitcher 主窗口。
/// </summary>
public partial class MainWindow : Window
{
    private const string AppLogoResourceUri = "pack://application:,,,/Assets/logo-reference-transparent.png";

    private bool operationLogScrollHooked;
    private bool isExitRequested;
    private Forms.NotifyIcon? trayIcon;

    /// <summary>
    /// 初始化主窗口。
    /// </summary>
    public MainWindow()
    {
        InitializeComponent();
        var viewModel = new MainWindowViewModel();
        DataContext = viewModel;
        InitializeTrayIcon(viewModel);
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

    /// <inheritdoc />
    protected override void OnClosing(CancelEventArgs e)
    {
        if (!isExitRequested)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        base.OnClosing(e);
    }

    /// <inheritdoc />
    protected override void OnClosed(System.EventArgs e)
    {
        trayIcon?.Dispose();
        trayIcon = null;
        base.OnClosed(e);
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

    private void InitializeTrayIcon(MainWindowViewModel viewModel)
    {
        var menu = new Forms.ContextMenuStrip
        {
            BackColor = Drawing.Color.FromArgb(16, 27, 40),
            ForeColor = Drawing.Color.FromArgb(248, 250, 252),
            Font = new Drawing.Font("Microsoft YaHei UI", 9f, Drawing.FontStyle.Regular, Drawing.GraphicsUnit.Point),
            Padding = new Forms.Padding(6, 5, 6, 5),
            ShowImageMargin = true,
            Renderer = new TrayMenuRenderer()
        };
        var showMainWindowItem = new Forms.ToolStripMenuItem("打开主窗口", CreateWindowIcon());
        var toggleWidgetItem = new Forms.ToolStripMenuItem("显示额度挂件", CreateWidgetIcon());
        var exitItem = new Forms.ToolStripMenuItem("退出程序", CreateExitIcon());
        ConfigureTrayMenuItem(showMainWindowItem);
        ConfigureTrayMenuItem(toggleWidgetItem);
        ConfigureTrayMenuItem(exitItem);

        showMainWindowItem.Click += (_, _) => Dispatcher.Invoke(ShowMainWindow);
        toggleWidgetItem.Click += (_, _) => Dispatcher.Invoke(() => viewModel.ToggleUsageWidgetCommand.Execute(null));
        exitItem.Click += (_, _) => Dispatcher.Invoke(ExitApplication);
        menu.Opening += (_, _) =>
        {
            toggleWidgetItem.Text = viewModel.IsUsageWidgetVisible ? "隐藏额度挂件" : "显示额度挂件";
        };

        menu.Items.Add(showMainWindowItem);
        menu.Items.Add(toggleWidgetItem);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(exitItem);

        trayIcon = new Forms.NotifyIcon
        {
            ContextMenuStrip = menu,
            Icon = LoadTrayIcon(),
            Text = "CodexAppSwitcher 正在后台运行",
            Visible = true
        };
        trayIcon.MouseClick += (_, e) =>
        {
            if (e.Button == Forms.MouseButtons.Left)
            {
                Dispatcher.Invoke(ShowMainWindow);
            }
        };
    }

    private void ShowMainWindow()
    {
        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }

        Show();
        Activate();
    }

    private void ExitApplication()
    {
        isExitRequested = true;
        if (trayIcon is not null)
        {
            trayIcon.Visible = false;
        }

        Application.Current.Shutdown();
    }

    private static Drawing.Icon LoadTrayIcon()
    {
        try
        {
            return CreateIconFromLogoResource(32);
        }
        catch (Exception)
        {
            return LoadExecutableIconFallback();
        }
    }

    private static Drawing.Icon LoadExecutableIconFallback()
    {
        try
        {
            var executablePath = Process.GetCurrentProcess().MainModule?.FileName;
            return executablePath is null
                ? Drawing.SystemIcons.Application
                : Drawing.Icon.ExtractAssociatedIcon(executablePath) ?? Drawing.SystemIcons.Application;
        }
        catch (Exception)
        {
            return Drawing.SystemIcons.Application;
        }
    }

    private static Drawing.Icon CreateIconFromLogoResource(int size)
    {
        var resource = Application.GetResourceStream(new Uri(AppLogoResourceUri, UriKind.Absolute))
            ?? throw new InvalidOperationException("未找到应用 Logo 资源。");

        using var sourceStream = resource.Stream;
        using var sourceBitmap = new Drawing.Bitmap(sourceStream);
        using var iconBitmap = new Drawing.Bitmap(size, size, Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (var graphics = Drawing.Graphics.FromImage(iconBitmap))
        {
            graphics.Clear(Drawing.Color.Transparent);
            graphics.SmoothingMode = Drawing.Drawing2D.SmoothingMode.AntiAlias;
            graphics.InterpolationMode = Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            graphics.PixelOffsetMode = Drawing.Drawing2D.PixelOffsetMode.HighQuality;
            graphics.DrawImage(sourceBitmap, 0, 0, size, size);
        }

        var iconHandle = iconBitmap.GetHicon();
        try
        {
            using var icon = Drawing.Icon.FromHandle(iconHandle);
            return (Drawing.Icon)icon.Clone();
        }
        finally
        {
            DestroyIcon(iconHandle);
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr handle);

    private static void ConfigureTrayMenuItem(Forms.ToolStripMenuItem item)
    {
        item.ImageScaling = Forms.ToolStripItemImageScaling.None;
        item.Padding = new Forms.Padding(2, 4, 12, 4);
        item.Margin = new Forms.Padding(2, 1, 2, 1);
    }

    private static Drawing.Bitmap CreateWindowIcon()
    {
        return CreateMenuIcon(graphics =>
        {
            using var borderPen = new Drawing.Pen(Drawing.Color.FromArgb(148, 163, 184), 1.8f);
            using var accentPen = new Drawing.Pen(Drawing.Color.FromArgb(85, 214, 190), 1.6f);
            graphics.DrawRectangle(borderPen, 4, 6, 16, 12);
            graphics.DrawLine(accentPen, 7, 10, 17, 10);
        });
    }

    private static Drawing.Bitmap CreateWidgetIcon()
    {
        return CreateMenuIcon(graphics =>
        {
            using var accentPen = new Drawing.Pen(Drawing.Color.FromArgb(85, 214, 190), 2f);
            using var mutedPen = new Drawing.Pen(Drawing.Color.FromArgb(71, 85, 105), 1.5f);
            graphics.DrawEllipse(mutedPen, 4, 4, 16, 16);
            graphics.DrawArc(accentPen, 4, 4, 16, 16, -90, 270);
            using var textBrush = new Drawing.SolidBrush(Drawing.Color.FromArgb(248, 250, 252));
            using var font = new Drawing.Font("Segoe UI", 6.5f, Drawing.FontStyle.Bold, Drawing.GraphicsUnit.Point);
            graphics.DrawString("%", font, textBrush, 7.1f, 6.6f);
        });
    }

    private static Drawing.Bitmap CreateExitIcon()
    {
        return CreateMenuIcon(graphics =>
        {
            using var dangerPen = new Drawing.Pen(Drawing.Color.FromArgb(239, 68, 68), 2f)
            {
                StartCap = Drawing.Drawing2D.LineCap.Round,
                EndCap = Drawing.Drawing2D.LineCap.Round
            };
            graphics.DrawLine(dangerPen, 7, 7, 17, 17);
            graphics.DrawLine(dangerPen, 17, 7, 7, 17);
        });
    }

    private static Drawing.Bitmap CreateMenuIcon(Action<Drawing.Graphics> draw)
    {
        var bitmap = new Drawing.Bitmap(24, 24);
        using var graphics = Drawing.Graphics.FromImage(bitmap);
        graphics.SmoothingMode = Drawing.Drawing2D.SmoothingMode.AntiAlias;
        graphics.Clear(Drawing.Color.Transparent);
        draw(graphics);
        return bitmap;
    }
}

internal sealed class TrayMenuRenderer : Forms.ToolStripProfessionalRenderer
{
    public TrayMenuRenderer()
        : base(new TrayMenuColorTable())
    {
        RoundedEdges = true;
    }

    protected override void OnRenderToolStripBorder(Forms.ToolStripRenderEventArgs e)
    {
        using var pen = new Drawing.Pen(Drawing.Color.FromArgb(34, 50, 68));
        var bounds = new Drawing.Rectangle(0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
        e.Graphics.DrawRectangle(pen, bounds);
    }

    protected override void OnRenderSeparator(Forms.ToolStripSeparatorRenderEventArgs e)
    {
        using var pen = new Drawing.Pen(Drawing.Color.FromArgb(51, 65, 85));
        var y = e.Item.Height / 2;
        e.Graphics.DrawLine(pen, 34, y, e.Item.Width - 10, y);
    }

    protected override void OnRenderMenuItemBackground(Forms.ToolStripItemRenderEventArgs e)
    {
        if (!e.Item.Selected)
        {
            return;
        }

        var isExitItem = e.Item.Text?.Contains("退出程序", StringComparison.Ordinal) == true;
        var fillColor = isExitItem
            ? Drawing.Color.FromArgb(68, 28, 35)
            : Drawing.Color.FromArgb(38, 54, 77);
        var borderColor = isExitItem
            ? Drawing.Color.FromArgb(239, 68, 68)
            : Drawing.Color.FromArgb(85, 214, 190);

        using var fillBrush = new Drawing.SolidBrush(fillColor);
        using var borderPen = new Drawing.Pen(borderColor);
        var bounds = new Drawing.Rectangle(2, 1, e.Item.Width - 4, e.Item.Height - 2);
        e.Graphics.FillRectangle(fillBrush, bounds);
        e.Graphics.DrawRectangle(borderPen, bounds);
    }
}

internal sealed class TrayMenuColorTable : Forms.ProfessionalColorTable
{
    public override Drawing.Color ToolStripDropDownBackground => Drawing.Color.FromArgb(16, 27, 40);

    public override Drawing.Color ImageMarginGradientBegin => Drawing.Color.FromArgb(16, 27, 40);

    public override Drawing.Color ImageMarginGradientMiddle => Drawing.Color.FromArgb(16, 27, 40);

    public override Drawing.Color ImageMarginGradientEnd => Drawing.Color.FromArgb(16, 27, 40);

    public override Drawing.Color MenuItemSelected => Drawing.Color.FromArgb(38, 54, 77);

    public override Drawing.Color MenuItemSelectedGradientBegin => Drawing.Color.FromArgb(38, 54, 77);

    public override Drawing.Color MenuItemSelectedGradientEnd => Drawing.Color.FromArgb(38, 54, 77);

    public override Drawing.Color MenuItemBorder => Drawing.Color.FromArgb(85, 214, 190);
}
