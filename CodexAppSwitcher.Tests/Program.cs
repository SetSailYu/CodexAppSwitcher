using System;
using System.IO;
using CodexAppSwitcher.Models;
using CodexAppSwitcher.Services;

namespace CodexAppSwitcher.Tests;

/// <summary>
/// 不依赖外部测试框架的最小测试入口。
/// </summary>
public static class Program
{
    /// <summary>
    /// 执行所有测试。失败时返回非零退出码。
    /// </summary>
    public static int Main()
    {
        var root = Path.Combine(Path.GetTempPath(), "CodexAppSwitcher.Tests", Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(root);
            TestExecuteSwitchReplacesAuthState(root);
            TestSwitchRejectsMissingSnapshot(root);
            TestSwitchServiceOnlyRequiresTargetSnapshot(root);
            TestSwitchRejectsLockedAuthJson(root);
            TestSwitchWritesOnlyLiveAuth(root);
            TestSwitchDoesNotRollbackWhenApplyFails(root);
            TestRoamingPathDoesNotFallbackToPackageState(root);
            TestRoamingPathUsesExistingChatGptCandidate(root);
            TestExecuteSwitchDoesNotReplacePackageState(root);
            TestCodexProcessNameIncludesChatGptShell();
            TestSnapshotCaptureRejectsOverwriteByDefault(root);
            TestSnapshotCaptureOverwritesWhenConfirmed(root);
            TestSnapshotCaptureAcceptsMinimalAuthState(root);
            TestSnapshotCaptureKeepsValidFilesWhenRuntimeFileIsLocked(root);
            TestSnapshotCaptureDoesNotIncludePackageState(root);
            TestUsageParserReadsChineseAnalyticsText();
            TestUsageParserReadsChatGptCodexUsageDialogText();
            TestUsageParserReadsWhamUsageApiJson();
            TestUsageParserClampsPercentAndReadsEnglishText();
            TestUsageParserRejectsIncompleteText();
            Console.WriteLine("全部测试通过。");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private static void TestExecuteSwitchReplacesAuthState(string root)
    {
        var context = CreateContext(Path.Combine(root, "execute"));
        SeedCurrentLoginState(context.Paths, "source");
        SeedTargetSnapshot(context.Paths, "target", "target");

        var result = context.SwitchService.ExecuteSwitch("source", "target");

        Assert(result.IsSuccess, result.Message);
        Assert(File.ReadAllText(Path.Combine(context.Paths.RoamingCodexPath, "state.txt")) == "source", "直接切换不应替换 Roaming 附加状态。");
        Assert(File.ReadAllText(context.Paths.AuthJsonPath) == "target-auth", "直接切换应替换 auth.json。");
        Assert(!Directory.Exists(Path.Combine(context.Paths.SwitcherDataRoot, "backups")), "直接切换不应创建 rollback。");
    }

    private static void TestSwitchRejectsMissingSnapshot(string root)
    {
        var context = CreateContext(Path.Combine(root, "missing-snapshot"));
        SeedCurrentLoginState(context.Paths, "source");

        var result = context.SwitchService.ExecuteSwitch("source", "target");

        Assert(!result.IsSuccess, "缺失目标快照时不应允许切换。");
        Assert(result.Message.Contains("缺少 Codex auth.json 快照", StringComparison.Ordinal), result.Message);
        Assert(File.ReadAllText(Path.Combine(context.Paths.RoamingCodexPath, "state.txt")) == "source", "缺失快照失败后不应修改当前登录态。");
    }

    private static void TestSwitchServiceOnlyRequiresTargetSnapshot(string root)
    {
        var context = CreateContext(Path.Combine(root, "service-boundary"));
        SeedCurrentLoginState(context.Paths, "source");
        SeedTargetSnapshot(context.Paths, "target", "target");

        var result = context.SwitchService.ExecuteSwitch("source", "target");

        Assert(result.IsSuccess, result.Message);
        Assert(File.ReadAllText(context.Paths.AuthJsonPath) == "target-auth", "切换服务只应按目标快照写入 auth.json。");
    }

    private static void TestSwitchRejectsLockedAuthJson(string root)
    {
        var context = CreateContext(Path.Combine(root, "locked-auth"));
        SeedCurrentLoginState(context.Paths, "source");
        SeedTargetSnapshot(context.Paths, "target", "target");

        using var authLock = new FileStream(context.Paths.AuthJsonPath, FileMode.Open, FileAccess.Read, FileShare.None);
        var result = context.SwitchService.ExecuteSwitch("source", "target");

        Assert(!result.IsSuccess, "auth.json 被占用时不应允许直接切换。");
    }

    private static void TestSwitchWritesOnlyLiveAuth(string root)
    {
        var context = CreateContext(Path.Combine(root, "live-auth-only"));
        SeedCurrentLoginState(context.Paths, "source");
        SeedTargetSnapshot(context.Paths, "target", "target");

        var result = context.SwitchService.ExecuteSwitch("source", "target");

        Assert(result.IsSuccess, result.Message);
        Assert(result.Message.Contains("仅写 live auth.json", StringComparison.Ordinal), result.Message);
        Assert(!result.Message.Contains("token", StringComparison.OrdinalIgnoreCase), result.Message);
        Assert(File.ReadAllText(context.Paths.AuthJsonPath) == "target-auth", "切换只应写入 live auth.json。");
    }


    private static void TestSwitchDoesNotRollbackWhenApplyFails(string root)
    {
        var context = CreateContext(Path.Combine(root, "restore-after-failure"));
        SeedCurrentLoginState(context.Paths, "source");
        SeedTargetSnapshot(context.Paths, "target", "target");
        var lockedSnapshotFile = Path.Combine(
            context.Paths.GetAccountLoginStatePath("target"),
            "auth.json");

        using var snapshotLock = new FileStream(lockedSnapshotFile, FileMode.Open, FileAccess.Read, FileShare.None);
        var result = context.SwitchService.ExecuteSwitch("source", "target");

        Assert(!result.IsSuccess, "目标快照文件被占用时不应完成切换。");
        Assert(!result.Message.Contains("rollback", StringComparison.OrdinalIgnoreCase), result.Message);
        Assert(File.ReadAllText(Path.Combine(context.Paths.RoamingCodexPath, "state.txt")) == "source", "切换失败后应保持源账号 Roaming 附加状态。");
        Assert(File.ReadAllText(context.Paths.AuthJsonPath) == "source-auth", "目标快照读取失败时 live auth.json 应保持原内容。");
    }

    private static void TestExecuteSwitchDoesNotReplacePackageState(string root)
    {
        var context = CreateContext(Path.Combine(root, "execute-package"), includePackageState: true);
        SeedCurrentLoginState(context.Paths, "source");
        SeedPackageState(context.PackageStatePath!, "source-package");
        SeedTargetSnapshot(context.Paths, "target", "target", "target-package");

        var result = context.SwitchService.ExecuteSwitch("source", "target");

        Assert(result.IsSuccess, result.Message);
        Assert(File.ReadAllText(Path.Combine(context.PackageStatePath!, "package.txt")) == "source-package", "切换不应替换 MSIX 包状态。");
        Assert(!Directory.Exists(Path.Combine(context.Paths.SwitcherDataRoot, "backups")), "直接切换不应创建 rollback。");
    }

    private static void TestRoamingPathDoesNotFallbackToPackageState(string root)
    {
        var context = CreateContext(Path.Combine(root, "roaming-not-package"), includePackageState: true);
        SeedPackageState(context.PackageStatePath!, "package");

        Assert(
            context.Paths.RoamingCodexPath.EndsWith(Path.Combine("AppData", "Roaming", "Codex"), StringComparison.Ordinal),
            "主登录态目录必须保持为 AppData Roaming Codex，不能退到 MSIX 包状态目录。");
    }

    private static void TestRoamingPathUsesExistingChatGptCandidate(string root)
    {
        var userRoot = Path.Combine(root, "roaming-chatgpt-candidate", "user");
        var chatGptRoamingPath = Path.Combine(userRoot, "AppData", "Roaming", "ChatGPT");
        Directory.CreateDirectory(chatGptRoamingPath);
        var paths = new CodexPathResolver(new CodexPathOptions
        {
            UserProfileRootOverride = userRoot,
            RoamingCodexRelativePath = Path.Combine("AppData", "Roaming", "Codex")
        });

        Assert(paths.RoamingCodexPath == chatGptRoamingPath, "存在 ChatGPT Roaming 候选时应优先使用新版目录。");
    }

    private static void TestCodexProcessNameIncludesChatGptShell()
    {
        Assert(CodexProcessService.IsCodexProcessName("ChatGPT"), "新版 ChatGPT 壳进程应被识别为 Codex App。");
        Assert(CodexProcessService.IsCodexProcessName("codex"), "Codex CLI 子进程应继续被识别。");
        Assert(!CodexProcessService.IsCodexProcessName("codex-command-runner-0.144.0-alpha.4"), "不应误伤 Codex 辅助命令执行进程。");
    }

    private static void TestSnapshotCaptureRejectsOverwriteByDefault(string root)
    {
        var context = CreateContext(Path.Combine(root, "snapshot-reject-overwrite"));
        SeedCurrentLoginState(context.Paths, "new");
        SeedTargetSnapshot(context.Paths, "account", "old");

        var result = context.SnapshotService.CaptureCurrentLoginState("account");

        Assert(!result.IsSuccess, "默认不应覆盖既有账号快照。");
        Assert(result.Message.Contains("未确认覆盖", StringComparison.Ordinal), result.Message);
        Assert(File.ReadAllText(Path.Combine(context.Paths.GetAccountLoginStatePath("account"), "roaming-codex", "state.txt")) == "old", "拒绝覆盖后旧快照应保持不变。");
        Assert(File.ReadAllText(Path.Combine(context.Paths.GetAccountLoginStatePath("account"), "auth.json")) == "old-auth", "拒绝覆盖后旧 auth 快照应保持不变。");
    }

    private static void TestSnapshotCaptureOverwritesWhenConfirmed(string root)
    {
        var context = CreateContext(Path.Combine(root, "snapshot-overwrite"));
        SeedCurrentLoginState(context.Paths, "new");
        SeedTargetSnapshot(context.Paths, "account", "old");

        var result = context.SnapshotService.CaptureCurrentLoginState("account", allowOverwrite: true);

        Assert(result.IsSuccess, result.Message);
        Assert(File.ReadAllText(Path.Combine(context.Paths.GetAccountLoginStatePath("account"), "roaming-codex", "state.txt")) == "new", "确认覆盖后应写入新的 Roaming 快照。");
        Assert(File.ReadAllText(Path.Combine(context.Paths.GetAccountLoginStatePath("account"), "auth.json")) == "new-auth", "确认覆盖后应写入新的 auth 快照。");
        Assert(!Directory.Exists($"{context.Paths.GetAccountLoginStatePath("account")}.staging"), "覆盖成功后不应残留 staging 目录。");
        Assert(!Directory.Exists($"{context.Paths.GetAccountLoginStatePath("account")}.backup"), "覆盖成功后不应残留 backup 目录。");
    }

    private static void TestSnapshotCaptureDoesNotIncludePackageState(string root)
    {
        var context = CreateContext(Path.Combine(root, "snapshot-package"), includePackageState: true);
        SeedCurrentLoginState(context.Paths, "new");
        SeedPackageState(context.PackageStatePath!, "package");

        var result = context.SnapshotService.CaptureCurrentLoginState("account");

        Assert(result.IsSuccess, result.Message);
        Assert(!Directory.Exists(Path.Combine(context.Paths.GetAccountLoginStatePath("account"), "package-state")), "采集快照时不应再保存 MSIX 包状态。");
    }

    private static void TestSnapshotCaptureAcceptsMinimalAuthState(string root)
    {
        var context = CreateContext(Path.Combine(root, "snapshot-empty"));
        Directory.CreateDirectory(context.Paths.RoamingCodexPath);
        Directory.CreateDirectory(Path.GetDirectoryName(context.Paths.AuthJsonPath)!);
        File.WriteAllText(context.Paths.AuthJsonPath, "auth");

        var result = context.SnapshotService.CaptureCurrentLoginState("account");

        Assert(result.IsSuccess, result.Message);
        Assert(result.Message.Contains("auth.json 已保存", StringComparison.Ordinal), result.Message);
        Assert(result.Message.Contains("源候选 0 个", StringComparison.Ordinal), result.Message);
        Assert(File.ReadAllText(Path.Combine(context.Paths.GetAccountLoginStatePath("account"), "auth.json")) == "auth", "账号快照应写入 auth.json。");
    }

    private static void TestSnapshotCaptureKeepsValidFilesWhenRuntimeFileIsLocked(string root)
    {
        var context = CreateContext(Path.Combine(root, "snapshot-locked-runtime"));
        SeedCurrentLoginState(context.Paths, "new");
        var lockedRuntimeFile = Path.Combine(context.Paths.RoamingCodexPath, "runtime.db");
        File.WriteAllText(lockedRuntimeFile, "runtime");

        using var runtimeLock = new FileStream(lockedRuntimeFile, FileMode.Open, FileAccess.Read, FileShare.None);
        var result = context.SnapshotService.CaptureCurrentLoginState("account");

        Assert(result.IsSuccess, result.Message);
        Assert(File.ReadAllText(Path.Combine(context.Paths.GetAccountLoginStatePath("account"), "roaming-codex", "state.txt")) == "new", "运行期文件被占用时仍应保存可复制的登录态文件。");
    }

    private static void TestUsageParserReadsChineseAnalyticsText()
    {
        var snapshot = UsageStatusService.TryParseVisibleText("""
            5 小时使用限额
            4% 剩余
            重置时间：16:39
            每周使用限额
            9% 剩余
            重置时间：2026年6月25日 11:42
            剩余额度
            0
            """);

        if (snapshot is null)
        {
            throw new InvalidOperationException("中文 analytics 文本应可解析。");
        }

        Assert(snapshot.FiveHourRemainingPercent == 4, "5 小时剩余比例解析错误。");
        Assert(snapshot.WeeklyRemainingPercent == 9, "每周剩余比例解析错误。");
        Assert(snapshot.FiveHourResetText == "16:39", "5 小时重置时间解析错误。");
        Assert(snapshot.WeeklyResetText == "2026年6月25日 11:42", "每周重置时间解析错误。");
        Assert(snapshot.ExtraQuotaText == "0", "剩余额度解析错误。");
    }

    private static void TestUsageParserReadsChatGptCodexUsageDialogText()
    {
        var snapshot = UsageStatusService.TryParseVisibleText("""
            使用量
            5 小时使用限制
            将于 02:19 重置
            剩余 72%
            每周使用限额
            将于 7月18日 重置
            剩余 96%
            使用限额重置
            可用 3 次
            """);

        if (snapshot is null)
        {
            throw new InvalidOperationException("ChatGPT Codex 使用情况弹窗文本应可解析。");
        }

        Assert(snapshot.FiveHourRemainingPercent == 72, "Codex 弹窗 5 小时剩余比例解析错误。");
        Assert(snapshot.WeeklyRemainingPercent == 96, "Codex 弹窗每周剩余比例解析错误。");
        Assert(snapshot.FiveHourResetText == "02:19", "Codex 弹窗 5 小时重置时间解析错误。");
        Assert(snapshot.WeeklyResetText == "7月18日", "Codex 弹窗每周重置时间解析错误。");
        Assert(snapshot.ExtraQuotaText == "3", "Codex 弹窗可用重置次数解析错误。");
    }

    private static void TestUsageParserReadsWhamUsageApiJson()
    {
        var snapshot = UsageStatusService.TryParseUsageApiJson("""
            {
              "email": "yusetsail@gmail.com",
              "rate_limit": {
                "primary_window": {
                  "used_percent": 1,
                  "reset_at": 1783798194
                },
                "secondary_window": {
                  "used_percent": 15,
                  "reset_at": 1784366482
                }
              },
              "credits": {
                "balance": "0"
              },
              "rate_limit_reset_credits": {
                "available_count": 2
              }
            }
            """);

        if (snapshot is null)
        {
            throw new InvalidOperationException("wham usage API JSON 应可解析。");
        }

        var fiveHourReset = DateTimeOffset.FromUnixTimeSeconds(1783798194).LocalDateTime;
        var weeklyReset = DateTimeOffset.FromUnixTimeSeconds(1784366482).LocalDateTime;
        Assert(snapshot.FiveHourRemainingPercent == 99, "wham API 5 小时剩余比例解析错误。");
        Assert(snapshot.WeeklyRemainingPercent == 85, "wham API 每周剩余比例解析错误。");
        Assert(snapshot.FiveHourResetText == $"{fiveHourReset:yyyy年M月d日 H:mm}", "wham API 5 小时重置时间解析错误。");
        Assert(snapshot.WeeklyResetText == $"{weeklyReset:yyyy年M月d日 H:mm}", "wham API 每周重置时间解析错误。");
        Assert(snapshot.ExtraQuotaText == "0", "wham API 剩余额度解析错误。");
    }

    private static void TestUsageParserClampsPercentAndReadsEnglishText()
    {
        var snapshot = UsageStatusService.TryParseVisibleText("""
            5h limit
            120% remaining
            reset at: 18:00
            weekly limit
            8% remaining
            reset time: Friday
            extra quota: 3
            """);

        if (snapshot is null)
        {
            throw new InvalidOperationException("英文 analytics 文本应可解析。");
        }

        Assert(snapshot.FiveHourRemainingPercent == 100, "剩余比例应限制在 100 以内。");
        Assert(snapshot.WeeklyRemainingPercent == 8, "英文每周剩余比例解析错误。");
        Assert(snapshot.FiveHourResetText == "18:00", "英文 5 小时重置时间解析错误。");
        Assert(snapshot.WeeklyResetText == "Friday", "英文每周重置时间解析错误。");
        Assert(snapshot.ExtraQuotaText == "3", "英文剩余额度解析错误。");
    }

    private static void TestUsageParserRejectsIncompleteText()
    {
        var snapshot = UsageStatusService.TryParseVisibleText("""
            5 小时使用限额
            4% 剩余
            """);

        Assert(snapshot is null, "缺少第二个剩余百分比时不应误报用量。");
    }

    private static TestContext CreateContext(
        string root,
        bool includePackageState = false)
    {
        var paths = new CodexPathResolver(new CodexPathOptions
        {
            UserProfileRootOverride = Path.Combine(root, "user"),
            SwitcherDataRootOverride = Path.Combine(root, "data"),
            RoamingCodexRelativePath = Path.Combine("AppData", "Roaming", "Codex"),
            AuthJsonRelativePath = Path.Combine(".codex", "auth.json")
        });
        var snapshotService = new AccountSnapshotService(paths);
        var switchService = new AccountSwitchService(
            snapshotService,
            paths);

        var packageStatePath = includePackageState ? Path.Combine(root, "package-state") : null;
        return new TestContext(paths, snapshotService, switchService, packageStatePath);
    }

    private static void SeedCurrentLoginState(CodexPathResolver paths, string marker)
    {
        Directory.CreateDirectory(paths.RoamingCodexPath);
        Directory.CreateDirectory(Path.GetDirectoryName(paths.AuthJsonPath)!);
        File.WriteAllText(Path.Combine(paths.RoamingCodexPath, "state.txt"), marker);
        File.WriteAllText(paths.AuthJsonPath, $"{marker}-auth");
    }

    private static void SeedTargetSnapshot(CodexPathResolver paths, string accountId, string marker)
    {
        SeedTargetSnapshot(paths, accountId, marker, packageMarker: null);
    }

    private static void SeedTargetSnapshot(
        CodexPathResolver paths,
        string accountId,
        string marker,
        string? packageMarker)
    {
        var snapshotRoot = paths.GetAccountLoginStatePath(accountId);
        var roamingSnapshot = Path.Combine(snapshotRoot, "roaming-codex");
        Directory.CreateDirectory(roamingSnapshot);
        File.WriteAllText(Path.Combine(roamingSnapshot, "state.txt"), marker);
        File.WriteAllText(Path.Combine(snapshotRoot, "auth.json"), $"{marker}-auth");
        if (packageMarker is not null)
        {
            var packageSnapshot = Path.Combine(snapshotRoot, "package-state");
            Directory.CreateDirectory(packageSnapshot);
            File.WriteAllText(Path.Combine(packageSnapshot, "package.txt"), packageMarker);
        }
    }

    private static void SeedPackageState(string packageStatePath, string marker)
    {
        Directory.CreateDirectory(packageStatePath);
        File.WriteAllText(Path.Combine(packageStatePath, "package.txt"), marker);
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed record TestContext(
        CodexPathResolver Paths,
        AccountSnapshotService SnapshotService,
        AccountSwitchService SwitchService,
        string? PackageStatePath);

}
