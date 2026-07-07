namespace CodexAppSwitcher.Models;

/// <summary>
/// 功能开关配置。
/// </summary>
public sealed class FeatureFlagOptions
{
    /// <summary>
    /// 是否允许刷新真实用量。默认必须关闭。
    /// </summary>
    public bool EnableUsageRefresh { get; set; }
}
