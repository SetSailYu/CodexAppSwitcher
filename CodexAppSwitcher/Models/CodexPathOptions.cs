namespace CodexAppSwitcher.Models;

/// <summary>
/// Codex 登录态路径配置。
/// </summary>
public sealed class CodexPathOptions
{
    /// <summary>
    /// 用户目录覆盖路径。为空时使用当前 Windows 用户目录。
    /// </summary>
    public string? UserProfileRootOverride { get; set; }

    /// <summary>
    /// Switcher 数据根目录覆盖路径。为空时使用 AppData\Roaming\CodexAccountSwitcher。
    /// </summary>
    public string? SwitcherDataRootOverride { get; set; }

    /// <summary>
    /// Codex App Roaming 目录相对用户目录路径。
    /// </summary>
    public string RoamingCodexRelativePath { get; set; } = "AppData\\Roaming\\Codex";

    /// <summary>
    /// Codex auth.json 相对用户目录路径。
    /// </summary>
    public string AuthJsonRelativePath { get; set; } = ".codex\\auth.json";
}
