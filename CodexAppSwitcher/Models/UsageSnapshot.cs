using System;

namespace CodexAppSwitcher.Models;

/// <summary>
/// Codex 用量快照。
/// </summary>
public sealed class UsageSnapshot
{
    /// <summary>
    /// 五小时额度剩余百分比。
    /// </summary>
    public int FiveHourRemainingPercent { get; set; }

    /// <summary>
    /// 每周额度剩余百分比。
    /// </summary>
    public int WeeklyRemainingPercent { get; set; }

    /// <summary>
    /// 五小时额度重置时间文本。
    /// </summary>
    public string FiveHourResetText { get; set; } = "未知";

    /// <summary>
    /// 每周额度重置时间文本。
    /// </summary>
    public string WeeklyResetText { get; set; } = "未知";

    /// <summary>
    /// 剩余额度文本。
    /// </summary>
    public string ExtraQuotaText { get; set; } = "未知";

    /// <summary>
    /// 最近刷新时间。
    /// </summary>
    public DateTimeOffset? RefreshedAt { get; set; }
}
