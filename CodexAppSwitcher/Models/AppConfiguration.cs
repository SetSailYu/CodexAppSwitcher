namespace CodexAppSwitcher.Models;

/// <summary>
/// 应用配置根对象。
/// </summary>
public sealed class AppConfiguration
{
    /// <summary>
    /// 功能开关。
    /// </summary>
    public FeatureFlagOptions FeatureFlags { get; set; } = new();

    /// <summary>
    /// Codex 路径配置。
    /// </summary>
    public CodexPathOptions CodexPaths { get; set; } = new();

    /// <summary>
    /// 用量查询配置。
    /// </summary>
    public UsageOptions Usage { get; set; } = new();

    /// <summary>
    /// 日志配置。
    /// </summary>
    public LoggingOptions Logging { get; set; } = new();
}

/// <summary>
/// 用量查询配置。
/// </summary>
public sealed class UsageOptions
{
    /// <summary>
    /// Codex analytics 页面地址。
    /// </summary>
    public string AnalyticsUrl { get; set; } = "https://chatgpt.com/codex/cloud/settings/analytics";

    /// <summary>
    /// 页面加载等待秒数。
    /// </summary>
    public int PageLoadTimeoutSeconds { get; set; } = 30;
}

/// <summary>
/// 日志输出配置。
/// </summary>
public sealed class LoggingOptions
{
    /// <summary>
    /// 最低日志等级。
    /// </summary>
    public string Level { get; set; } = "Information";

    /// <summary>
    /// 输出格式。支持 console 或 json。
    /// </summary>
    public string Format { get; set; } = "console";
}
