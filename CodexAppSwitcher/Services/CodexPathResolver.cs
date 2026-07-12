using System;
using System.IO;
using System.Linq;
using CodexAppSwitcher.Models;

namespace CodexAppSwitcher.Services;

/// <summary>
/// 解析 Codex App 登录态相关路径。
/// </summary>
public sealed class CodexPathResolver
{
    private readonly CodexPathOptions _options;

    /// <summary>
    /// 创建路径解析器。
    /// </summary>
    public CodexPathResolver(CodexPathOptions options)
    {
        _options = options;
    }

    /// <summary>
    /// 获取当前 Windows 用户目录。
    /// </summary>
    public string UserProfilePath => string.IsNullOrWhiteSpace(_options.UserProfileRootOverride)
        ? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
        : Path.GetFullPath(_options.UserProfileRootOverride);

    /// <summary>
    /// 获取 Codex App Roaming 目录。
    /// </summary>
    public string RoamingCodexPath
    {
        get
        {
            if (IsDefaultRoamingRelativePath(_options.RoamingCodexRelativePath))
            {
                return RoamingCodexCandidatePaths.FirstOrDefault(Directory.Exists) ??
                    BuildRoamingPath("Codex");
            }

            var configuredPath = Path.Combine(UserProfilePath, _options.RoamingCodexRelativePath);
            return configuredPath;
        }
    }

    /// <summary>
    /// 鑾峰彇 Codex/ChatGPT App Roaming 鍊欓€夌洰褰曘€?
    /// </summary>
    public string[] RoamingCodexCandidatePaths =>
        [
            BuildRoamingPath("Codex"),
            BuildRoamingPath("ChatGPT"),
            BuildRoamingPath(Path.Combine("OpenAI", "Codex")),
            BuildRoamingPath(Path.Combine("OpenAI", "ChatGPT"))
        ];

    /// <summary>
    /// 获取 Codex auth.json 路径。
    /// </summary>
    public string AuthJsonPath
    {
        get
        {
            if (string.IsNullOrWhiteSpace(_options.UserProfileRootOverride))
            {
                var codexHome = Environment.GetEnvironmentVariable("CODEX_HOME");
                if (!string.IsNullOrWhiteSpace(codexHome) && !ContainsParentDirectory(codexHome))
                {
                    return Path.Combine(Path.GetFullPath(codexHome), "auth.json");
                }
            }

            return Path.Combine(UserProfilePath, _options.AuthJsonRelativePath);
        }
    }

    /// <summary>
    /// 获取 Switcher 数据根目录。
    /// </summary>
    public string SwitcherDataRoot => string.IsNullOrWhiteSpace(_options.SwitcherDataRootOverride)
        ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CodexAccountSwitcher")
        : Path.GetFullPath(_options.SwitcherDataRootOverride);

    /// <summary>
    /// 获取账号本地数据根目录。
    /// </summary>
    public string GetAccountRootPath(string accountId) =>
        Path.Combine(SwitcherDataRoot, "accounts", accountId);

    /// <summary>
    /// 获取账号登录态快照目录。
    /// </summary>
    public string GetAccountLoginStatePath(string accountId) =>
        Path.Combine(GetAccountRootPath(accountId), "login-state");

    /// <summary>
    /// 获取账号 WebView2 用户数据目录。
    /// </summary>
    public string GetAccountBrowserProfilePath(string accountId) =>
        Path.Combine(GetAccountRootPath(accountId), "webview-profile");

    private static bool ContainsParentDirectory(string path)
    {
        return path
            .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(component => string.Equals(component, "..", StringComparison.Ordinal));
    }

    private string BuildRoamingPath(string relativePath)
    {
        var appDataRoot = string.IsNullOrWhiteSpace(_options.UserProfileRootOverride)
            ? Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)
            : Path.Combine(UserProfilePath, "AppData", "Roaming");

        return Path.Combine(appDataRoot, relativePath);
    }

    private static bool IsDefaultRoamingRelativePath(string path)
    {
        var normalizedPath = path
            .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar)
            .Trim(Path.DirectorySeparatorChar);
        return string.Equals(
            normalizedPath,
            Path.Combine("AppData", "Roaming", "Codex"),
            StringComparison.OrdinalIgnoreCase);
    }

}
