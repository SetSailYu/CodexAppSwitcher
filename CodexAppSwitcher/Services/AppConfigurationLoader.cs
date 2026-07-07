using System;
using System.IO;
using CodexAppSwitcher.Models;
using Microsoft.Extensions.Configuration;

namespace CodexAppSwitcher.Services;

/// <summary>
/// 应用配置加载器。
/// </summary>
public static class AppConfigurationLoader
{
    /// <summary>
    /// 按环境变量、appsettings.json、代码默认值的优先级加载配置。
    /// </summary>
    public static AppConfiguration Load()
    {
        var basePath = AppContext.BaseDirectory;
        var builder = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
            .AddEnvironmentVariables(prefix: "CODEX_SWITCHER_");

        var root = builder.Build();
        var configuration = new AppConfiguration();
        root.Bind(configuration);
        return configuration;
    }

    /// <summary>
    /// 获取可展示的配置来源摘要。
    /// </summary>
    public static string GetConfigurationSummary()
    {
        var appsettingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        return File.Exists(appsettingsPath) ? "已加载 appsettings.json" : "使用代码默认配置";
    }
}
