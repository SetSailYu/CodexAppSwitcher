namespace CodexAppSwitcher.Models;

/// <summary>
/// 当前账号身份识别结果。
/// </summary>
public sealed class AccountIdentitySnapshot
{
    /// <summary>
    /// 账号用户标识，例如邮箱。
    /// </summary>
    public string UserIdentifier { get; set; } = string.Empty;

    /// <summary>
    /// 识别来源说明，不包含敏感内容。
    /// </summary>
    public string Source { get; set; } = "未识别";

    /// <summary>
    /// 是否识别到用户标识。
    /// </summary>
    public bool HasUserIdentifier => !string.IsNullOrWhiteSpace(UserIdentifier);
}
