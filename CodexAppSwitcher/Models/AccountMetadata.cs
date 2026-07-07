using System;

namespace CodexAppSwitcher.Models;

/// <summary>
/// 账号元数据，不包含 token、cookie 或密码。
/// </summary>
public sealed class AccountMetadata
{
    /// <summary>
    /// 账号唯一标识。
    /// </summary>
    public string AccountId { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// 用户自定义账号名称。
    /// </summary>
    public string DisplayName { get; set; } = "未命名账号";

    /// <summary>
    /// 账号用户标识，例如 ChatGPT 账号邮箱。
    /// </summary>
    public string UserIdentifier { get; set; } = string.Empty;

    /// <summary>
    /// 账号对应的 WebView2 用户数据目录。
    /// </summary>
    public string BrowserProfilePath { get; set; } = string.Empty;

    /// <summary>
    /// 非敏感账号提示，例如用户手动填写的备注。
    /// </summary>
    public string Hint { get; set; } = string.Empty;

    /// <summary>
    /// 是否为当前活动账号。
    /// </summary>
    public bool IsCurrent { get; set; }

    /// <summary>
    /// 创建时间。
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;

    /// <summary>
    /// 最近一次采集登录态时间。
    /// </summary>
    public DateTimeOffset? LastCapturedAt { get; set; }
}
