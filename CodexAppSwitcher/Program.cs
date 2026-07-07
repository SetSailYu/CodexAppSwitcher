using System;
using System.Windows;
using CodexAppSwitcher.Models;
using CodexAppSwitcher.Services;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Json;

namespace CodexAppSwitcher;

/// <summary>
/// 应用程序入口。
/// </summary>
public static class Program
{
    /// <summary>
    /// 启动 WPF 应用。
    /// </summary>
    [STAThread]
    public static void Main()
    {
        var configuration = AppConfigurationLoader.Load();
        Log.Logger = CreateLogger(configuration.Logging);

        try
        {
            Log.Information("配置加载完成：{Summary}", AppConfigurationLoader.GetConfigurationSummary());

            var app = new App();
            app.InitializeComponent();
            app.Run();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "CodexAppSwitcher 启动失败");
            MessageBox.Show("CodexAppSwitcher 启动失败，请查看日志。", "启动失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    private static ILogger CreateLogger(LoggingOptions options)
    {
        var minimumLevel = Enum.TryParse(options.Level, ignoreCase: true, out LogEventLevel parsedLevel)
            ? parsedLevel
            : LogEventLevel.Information;
        var loggerConfiguration = new LoggerConfiguration().MinimumLevel.Is(minimumLevel);
        return string.Equals(options.Format, "json", StringComparison.OrdinalIgnoreCase)
            ? loggerConfiguration.WriteTo.Console(new JsonFormatter()).CreateLogger()
            : loggerConfiguration.WriteTo.Console().CreateLogger();
    }
}
