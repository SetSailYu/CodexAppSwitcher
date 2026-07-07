using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using CodexAppSwitcher.Models;

namespace CodexAppSwitcher.Services;

/// <summary>
/// Codex App 进程检测服务。
/// </summary>
public sealed class CodexProcessService
{
    private const int GracefulCloseTimeoutMilliseconds = 5000;
    private const int LaunchTimeoutMilliseconds = 8000;
    private static readonly string[] ProcessNames = ["Codex", "codex"];
    private readonly Func<bool>? _codexRunningOverride;

    /// <summary>
    /// 创建 Codex App 进程检测服务。
    /// </summary>
    public CodexProcessService()
    {
    }

    /// <summary>
    /// 创建可注入运行状态的进程检测服务，供隔离测试使用。
    /// </summary>
    public CodexProcessService(Func<bool> codexRunningOverride)
    {
        _codexRunningOverride = codexRunningOverride;
    }

    /// <summary>
    /// 判断 Codex App 是否正在运行。
    /// </summary>
    public bool IsCodexRunning()
    {
        if (_codexRunningOverride is not null)
        {
            return _codexRunningOverride();
        }

        return Process.GetProcesses()
            .Any(process => ProcessNames.Contains(process.ProcessName));
    }

    /// <summary>
    /// 获取 Codex App 运行状态摘要。
    /// </summary>
    public string GetProcessSummary()
    {
        var processes = GetCodexProcesses();
        var visibleWindowCount = processes.Count(HasMainWindow);

        if (visibleWindowCount > 0)
        {
            return $"检测到 {visibleWindowCount} 个 Codex App 窗口，{processes.Length} 个相关进程。";
        }

        return processes.Length > 0
            ? $"检测到 {processes.Length} 个 Codex 后台进程，但未检测到 App 窗口。"
            : "未检测到 Codex App 进程。";
    }

    /// <summary>
    /// 判断 Codex App 主窗口是否存在。
    /// </summary>
    public bool IsCodexWindowOpen() =>
        GetCodexProcesses().Any(HasMainWindow);

    /// <summary>
    /// 启动 Codex App。
    /// </summary>
    public OperationResult StartCodexApp()
    {
        if (IsCodexWindowOpen())
        {
            return OperationResult.Success("Codex App 窗口已在运行。");
        }

        var failures = new List<string>();
        foreach (var candidate in FindLaunchCandidates())
        {
            try
            {
                Process.Start(candidate.StartInfo);
            }
            catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or FileNotFoundException)
            {
                failures.Add($"{candidate.Description}：{ex.Message}");
                continue;
            }

            if (WaitForCodexLaunch())
            {
                return OperationResult.Success($"Codex App 已启动（{candidate.Description}）。");
            }

            failures.Add($"{candidate.Description}：已发送启动请求，但未检测到运行进程");
        }

        if (failures.Count == 0)
        {
            return OperationResult.Failure("未找到 Codex App 启动入口，请手动启动 Codex App。");
        }

        return OperationResult.Failure($"Codex App 自动启动失败，请手动启动。失败原因：{string.Join("；", failures.Take(3))}");
    }

    private bool WaitForCodexLaunch()
    {
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.ElapsedMilliseconds < LaunchTimeoutMilliseconds)
        {
            if (IsCodexWindowOpen())
            {
                return true;
            }

            Thread.Sleep(250);
        }

        return false;
    }

    /// <summary>
    /// 为账号切换关闭 Codex App。先请求窗口正常关闭，超时后结束残留进程树。
    /// </summary>
    public OperationResult CloseForSwitch()
    {
        var processes = GetCodexProcesses();
        if (processes.Length == 0)
        {
            return OperationResult.Success("未检测到需要关闭的 Codex App 进程。");
        }

        foreach (var process in processes.Where(HasMainWindow))
        {
            TryCloseMainWindow(process);
        }

        foreach (var process in processes)
        {
            WaitForExit(process);
        }

        var remainingProcesses = GetCodexProcesses();
        if (remainingProcesses.Length == 0)
        {
            return OperationResult.Success("Codex App 已关闭，可以继续切换。");
        }

        var killFailures = 0;
        foreach (var process in remainingProcesses)
        {
            if (!TryKillProcessTree(process))
            {
                killFailures++;
            }
        }

        foreach (var process in remainingProcesses)
        {
            WaitForExit(process);
        }

        var stillRunningCount = GetCodexProcesses().Length;
        if (stillRunningCount > 0)
        {
            return OperationResult.Failure($"Codex App 仍有 {stillRunningCount} 个进程未退出，请手动结束后再切换。");
        }

        return killFailures > 0
            ? OperationResult.Success("Codex App 已关闭，部分残留进程曾需强制结束。")
            : OperationResult.Success("Codex App 已关闭，残留进程已结束，可以继续切换。");
    }

    private static bool TryCloseMainWindow(Process process)
    {
        try
        {
            return process.CloseMainWindow();
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static Process[] GetCodexProcesses() =>
        Process.GetProcesses()
            .Where(process => ProcessNames.Contains(process.ProcessName))
            .ToArray();

    private static IEnumerable<LaunchCandidate> FindLaunchCandidates()
    {
        foreach (var appId in FindPackagedAppIds())
        {
            yield return new LaunchCandidate(
                "Windows Apps",
                new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"shell:AppsFolder\\{appId}",
                    UseShellExecute = true
                });
        }

        yield return new LaunchCandidate(
            "codex 协议",
            new ProcessStartInfo
            {
                FileName = "codex:",
                UseShellExecute = true
            });

        var shortcutPath = FindCodexShortcut();
        if (shortcutPath is not null)
        {
            yield return new LaunchCandidate(
                "开始菜单快捷方式",
                new ProcessStartInfo
                {
                    FileName = shortcutPath,
                    UseShellExecute = true
                });
        }

        var executablePath = FindCodexExecutable();
        if (executablePath is not null)
        {
            yield return new LaunchCandidate(
                "本地安装目录",
                new ProcessStartInfo
                {
                    FileName = executablePath,
                    UseShellExecute = true
                });
        }
    }

    private static string? FindCodexExecutable()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(localAppData))
        {
            return null;
        }

        var preferredPath = Path.Combine(localAppData, "OpenAI", "Codex", "bin", "codex.exe");
        if (File.Exists(preferredPath))
        {
            return preferredPath;
        }

        var searchRoot = Path.Combine(localAppData, "OpenAI", "Codex");
        if (!Directory.Exists(searchRoot))
        {
            return null;
        }

        try
        {
            return Directory.GetFiles(searchRoot, "codex.exe", SearchOption.AllDirectories)
                .OrderBy(path => path.Count(ch => ch == Path.DirectorySeparatorChar))
                .ThenByDescending(path => File.GetLastWriteTimeUtc(path))
                .FirstOrDefault();
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            return null;
        }
    }

    private static string? FindCodexShortcut()
    {
        var shortcuts = new[]
            {
                Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
                Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu)
            }
            .Where(path => !string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
            .SelectMany(EnumerateShortcutFiles)
            .Where(path =>
            {
                var name = Path.GetFileNameWithoutExtension(path);
                return name.Contains("Codex", StringComparison.OrdinalIgnoreCase) &&
                    !name.Contains("Switcher", StringComparison.OrdinalIgnoreCase);
            })
            .OrderBy(GetShortcutScore)
            .ThenBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return shortcuts.FirstOrDefault();
    }

    private static string[] EnumerateShortcutFiles(string rootPath)
    {
        try
        {
            return Directory.GetFiles(rootPath, "*.lnk", SearchOption.AllDirectories);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            return [];
        }
    }

    private static int GetShortcutScore(string shortcutPath)
    {
        var name = Path.GetFileNameWithoutExtension(shortcutPath);
        if (string.Equals(name, "Codex", StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        return name.Contains("OpenAI", StringComparison.OrdinalIgnoreCase) ? 1 : 2;
    }

    private static string[] FindPackagedAppIds()
    {
        var programFilesRoot = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        if (string.IsNullOrWhiteSpace(programFilesRoot))
        {
            return [];
        }

        var windowsAppsRoot = Path.Combine(programFilesRoot, "WindowsApps");
        if (!Directory.Exists(windowsAppsRoot))
        {
            return [];
        }

        try
        {
            return Directory.GetDirectories(windowsAppsRoot, "OpenAI.Codex_*", SearchOption.TopDirectoryOnly)
                .Select(Path.GetFileName)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(TryBuildAppUserModelId)
                .Where(appId => !string.IsNullOrWhiteSpace(appId))
                .ToArray()!;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            return [];
        }
    }

    private static string? TryBuildAppUserModelId(string? packageDirectoryName)
    {
        if (string.IsNullOrWhiteSpace(packageDirectoryName))
        {
            return null;
        }

        var doubleUnderscoreIndex = packageDirectoryName.IndexOf("__", StringComparison.Ordinal);
        if (doubleUnderscoreIndex < 0 || doubleUnderscoreIndex + 2 >= packageDirectoryName.Length)
        {
            return null;
        }

        var familySuffix = packageDirectoryName[(doubleUnderscoreIndex + 2)..];
        return $"OpenAI.Codex_{familySuffix}!App";
    }

    private static bool HasMainWindow(Process process)
    {
        try
        {
            return process.MainWindowHandle != IntPtr.Zero;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static bool TryKillProcessTree(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
            return true;
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception or NotSupportedException)
        {
            return false;
        }
    }

    private static void WaitForExit(Process process)
    {
        try
        {
            process.WaitForExit(GracefulCloseTimeoutMilliseconds);
        }
        catch (InvalidOperationException)
        {
        }
    }

    private sealed record LaunchCandidate(string Description, ProcessStartInfo StartInfo);
}
