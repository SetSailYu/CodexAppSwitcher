namespace CodexAppSwitcher.ViewModels;

/// <summary>
/// 账号行展示模型。
/// </summary>
public sealed class AccountRowViewModel
{
    /// <summary>
    /// 创建账号行。
    /// </summary>
    public AccountRowViewModel(
        string accountId,
        string displayName,
        string accountHint,
        string healthText,
        int? fiveHourRemainingPercent,
        int? weeklyRemainingPercent,
        string fiveHourResetText,
        string weeklyResetText,
        bool isCurrent,
        bool isCapturing,
        bool isSwitching,
        bool isTakeoverPending)
    {
        AccountId = accountId;
        DisplayName = displayName;
        AccountHint = accountHint;
        HealthText = healthText;
        FiveHourRemainingPercent = fiveHourRemainingPercent;
        WeeklyRemainingPercent = weeklyRemainingPercent;
        FiveHourResetText = fiveHourResetText;
        WeeklyResetText = weeklyResetText;
        IsCurrent = isCurrent;
        IsCapturing = isCapturing;
        IsSwitching = isSwitching;
        IsTakeoverPending = isTakeoverPending;
    }

    /// <summary>
    /// 账号唯一标识。
    /// </summary>
    public string AccountId { get; }

    /// <summary>
    /// 账号显示名。
    /// </summary>
    public string DisplayName { get; }

    /// <summary>
    /// 账号提示信息。
    /// </summary>
    public string AccountHint { get; }

    /// <summary>
    /// 健康状态文本。
    /// </summary>
    public string HealthText { get; }

    /// <summary>
    /// 五小时额度剩余百分比。
    /// </summary>
    public int? FiveHourRemainingPercent { get; }

    /// <summary>
    /// 每周额度剩余百分比。
    /// </summary>
    public int? WeeklyRemainingPercent { get; }

    /// <summary>
    /// 五小时进度条数值。
    /// </summary>
    public int FiveHourProgressValue => FiveHourRemainingPercent ?? 0;

    /// <summary>
    /// 每周进度条数值。
    /// </summary>
    public int WeeklyProgressValue => WeeklyRemainingPercent ?? 0;

    /// <summary>
    /// 五小时额度是否低于警告阈值。
    /// </summary>
    public bool IsFiveHourUsageWarning => IsWarningUsage(FiveHourRemainingPercent);

    /// <summary>
    /// 五小时额度是否低于危险阈值。
    /// </summary>
    public bool IsFiveHourUsageCritical => IsCriticalUsage(FiveHourRemainingPercent);

    /// <summary>
    /// 每周额度是否低于警告阈值。
    /// </summary>
    public bool IsWeeklyUsageWarning => IsWarningUsage(WeeklyRemainingPercent);

    /// <summary>
    /// 每周额度是否低于危险阈值。
    /// </summary>
    public bool IsWeeklyUsageCritical => IsCriticalUsage(WeeklyRemainingPercent);

    /// <summary>
    /// 是否已有真实用量数据。
    /// </summary>
    public bool HasUsageData => FiveHourRemainingPercent.HasValue && WeeklyRemainingPercent.HasValue;

    /// <summary>
    /// 是否为当前活动账号。
    /// </summary>
    public bool IsCurrent { get; }

    /// <summary>
    /// 是否正在采集当前 Codex App 登录态。
    /// </summary>
    public bool IsCapturing { get; }

    /// <summary>
    /// 是否正在切换到当前账号。
    /// </summary>
    public bool IsSwitching { get; }

    /// <summary>
    /// 是否正在等待完成接管。
    /// </summary>
    public bool IsTakeoverPending { get; }

    /// <summary>
    /// 是否已经采集过 Codex App 登录态快照。
    /// </summary>
    public bool IsManaged => HealthText == "已接管";

    /// <summary>
    /// 接管按钮文本。
    /// </summary>
    public string TakeoverActionText => IsManaged ? "重接管" : "接管";

    /// <summary>
    /// 五小时额度重置时间展示文本。
    /// </summary>
    public string FiveHourResetText { get; }

    /// <summary>
    /// 每周额度重置时间展示文本。
    /// </summary>
    public string WeeklyResetText { get; }

    /// <summary>
    /// 五小时额度展示文本。
    /// </summary>
    public string FiveHourText => FiveHourRemainingPercent.HasValue ? $"{FiveHourRemainingPercent.Value} %" : "--";

    /// <summary>
    /// 每周额度展示文本。
    /// </summary>
    public string WeeklyText => WeeklyRemainingPercent.HasValue ? $"{WeeklyRemainingPercent.Value} %" : "--";

    private static bool IsWarningUsage(int? value)
    {
        return value.HasValue && value.Value >= 10 && value.Value < 25;
    }

    private static bool IsCriticalUsage(int? value)
    {
        return value.HasValue && value.Value < 10;
    }
}
