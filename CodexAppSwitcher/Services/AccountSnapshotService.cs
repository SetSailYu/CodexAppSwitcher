using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CodexAppSwitcher.Models;

namespace CodexAppSwitcher.Services;

/// <summary>
/// 账号登录态快照服务。
/// </summary>
public sealed class AccountSnapshotService
{
    private readonly CodexPathResolver _pathResolver;

    /// <summary>
    /// 创建账号快照服务。
    /// </summary>
    public AccountSnapshotService(CodexPathResolver pathResolver)
    {
        _pathResolver = pathResolver;
    }

    /// <summary>
    /// 检查当前 Codex 登录态是否具备采集条件。
    /// </summary>
    public OperationResult CheckCaptureReadiness()
    {
        if (!File.Exists(_pathResolver.AuthJsonPath))
        {
            return OperationResult.Failure("未发现 .codex\\auth.json。");
        }

        return OperationResult.Success("发现可采集的 Codex 登录态。");
    }

    /// <summary>
    /// 生成快照目标目录。本方法不执行复制。
    /// </summary>
    public string BuildSnapshotTargetPath(string accountId) =>
        _pathResolver.GetAccountLoginStatePath(accountId);

    /// <summary>
    /// 采集当前 Codex 登录态快照。默认不覆盖既有快照。
    /// </summary>
    public OperationResult CaptureCurrentLoginState(string accountId, bool allowOverwrite = false)
    {
        var readiness = CheckCaptureReadiness();
        if (!readiness.IsSuccess)
        {
            return readiness;
        }

        var targetPath = BuildSnapshotTargetPath(accountId);
        if (Directory.Exists(targetPath) && !allowOverwrite)
        {
            return OperationResult.Failure("账号快照已存在，未确认覆盖。");
        }

        try
        {
            var stagingPath = $"{targetPath}.staging";
            if (Directory.Exists(stagingPath))
            {
                Directory.Delete(stagingPath, recursive: true);
            }

            Directory.CreateDirectory(stagingPath);
            var authTarget = Path.Combine(stagingPath, "auth.json");
            File.Copy(_pathResolver.AuthJsonPath, authTarget, overwrite: false);

            var roamingTarget = Path.Combine(stagingPath, "roaming-codex");
            var roamingStats = CopyDirectory(_pathResolver.RoamingCodexPath, roamingTarget);
            var roamingFileCount = CountSnapshotFiles(roamingTarget);

            if (!Directory.Exists(targetPath))
            {
                Directory.Move(stagingPath, targetPath);
                return OperationResult.Success(BuildCaptureSuccessMessage(roamingFileCount, roamingStats));
            }

            var backupPath = $"{targetPath}.backup";
            if (Directory.Exists(backupPath))
            {
                Directory.Delete(backupPath, recursive: true);
            }

            try
            {
                Directory.Move(targetPath, backupPath);
                Directory.Move(stagingPath, targetPath);
                Directory.Delete(backupPath, recursive: true);
            }
            catch
            {
                if (!Directory.Exists(targetPath) && Directory.Exists(backupPath))
                {
                    Directory.Move(backupPath, targetPath);
                }

                throw;
            }
        }
        catch (IOException ex)
        {
            return OperationResult.Failure($"登录态快照采集失败：{ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            return OperationResult.Failure($"登录态快照采集失败：{ex.Message}");
        }

        return OperationResult.Success("登录态快照覆盖完成。");
    }

    private static SnapshotCopyStats CopyDirectory(string sourcePath, string targetPath)
    {
        var stats = new SnapshotCopyStats();
        Directory.CreateDirectory(targetPath);
        CopyDirectoryContents(sourcePath, targetPath, string.Empty, stats);
        return stats;
    }

    private static void CopyDirectoryContents(
        string sourcePath,
        string targetPath,
        string relativeRoot,
        SnapshotCopyStats stats)
    {
        var enumerationOptions = new EnumerationOptions
        {
            IgnoreInaccessible = true,
            RecurseSubdirectories = false
        };

        foreach (var file in EnumerateFiles(sourcePath, enumerationOptions, stats))
        {
            var relativePath = BuildRelativePath(relativeRoot, Path.GetFileName(file));
            if (ShouldSkipSnapshotPath(relativePath))
            {
                stats.SkippedFileCount++;
                continue;
            }

            stats.CandidateFileCount++;
            var targetFile = Path.Combine(targetPath, Path.GetFileName(file));
            Directory.CreateDirectory(Path.GetDirectoryName(targetFile)!);
            switch (TryCopyVolatileFile(file, targetFile, stats))
            {
                case SnapshotCopyOutcome.Copied:
                    stats.CopiedFileCount++;
                    break;
                case SnapshotCopyOutcome.Skipped:
                    stats.SkippedFileCount++;
                    break;
                case SnapshotCopyOutcome.Failed:
                    stats.FailedFileCount++;
                    break;
            }
        }

        foreach (var directory in EnumerateDirectories(sourcePath, enumerationOptions, stats))
        {
            var relativePath = BuildRelativePath(relativeRoot, Path.GetFileName(directory));
            if (ShouldSkipSnapshotPath(relativePath))
            {
                continue;
            }

            var targetDirectory = Path.Combine(targetPath, Path.GetFileName(directory));
            Directory.CreateDirectory(targetDirectory);
            CopyDirectoryContents(directory, targetDirectory, relativePath, stats);
        }
    }

    private static string BuildRelativePath(string relativeRoot, string fileName) =>
        string.IsNullOrWhiteSpace(relativeRoot)
            ? fileName
            : Path.Combine(relativeRoot, fileName);

    private static string[] EnumerateDirectories(
        string sourcePath,
        EnumerationOptions options,
        SnapshotCopyStats? stats = null)
    {
        try
        {
            return Directory.EnumerateDirectories(sourcePath, "*", options).ToArray();
        }
        catch (DirectoryNotFoundException)
        {
            return [];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            stats?.AddFailureSample(sourcePath, ex);
            return [];
        }
    }

    private static string[] EnumerateFiles(
        string sourcePath,
        EnumerationOptions options,
        SnapshotCopyStats? stats = null)
    {
        try
        {
            return Directory.EnumerateFiles(sourcePath, "*", options).ToArray();
        }
        catch (DirectoryNotFoundException)
        {
            return [];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            stats?.AddFailureSample(sourcePath, ex);
            return [];
        }
    }

    private static bool ShouldSkipSnapshotPath(string relativePath)
    {
        var normalizedPath = relativePath
            .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
        var fileName = Path.GetFileName(normalizedPath);
        if (fileName.Equals("LOCK", StringComparison.OrdinalIgnoreCase) ||
            fileName.Equals("lockfile", StringComparison.OrdinalIgnoreCase) ||
            fileName.EndsWith(".lock", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var firstSegment = normalizedPath.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return firstSegment is not null &&
            (firstSegment.Equals("Cache", StringComparison.OrdinalIgnoreCase) ||
            firstSegment.Equals("Code Cache", StringComparison.OrdinalIgnoreCase) ||
            firstSegment.Equals("Crashpad", StringComparison.OrdinalIgnoreCase) ||
            firstSegment.Equals("DawnGraphiteCache", StringComparison.OrdinalIgnoreCase) ||
            firstSegment.Equals("DawnWebGPUCache", StringComparison.OrdinalIgnoreCase) ||
            firstSegment.Equals("Extension State", StringComparison.OrdinalIgnoreCase) ||
            firstSegment.Equals("GPUCache", StringComparison.OrdinalIgnoreCase) ||
            firstSegment.Equals("Service Worker", StringComparison.OrdinalIgnoreCase) ||
            firstSegment.Equals("ShaderCache", StringComparison.OrdinalIgnoreCase) ||
            firstSegment.Equals("Shared Dictionary", StringComparison.OrdinalIgnoreCase));
    }

    private static SnapshotCopyOutcome TryCopyVolatileFile(
        string sourceFile,
        string targetFile,
        SnapshotCopyStats stats)
    {
        try
        {
            using var source = new FileStream(sourceFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var target = new FileStream(targetFile, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            source.CopyTo(target);
            return SnapshotCopyOutcome.Copied;
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            // Electron 运行时缓存文件可能在枚举后立即删除，跳过这类瞬时文件。
            return SnapshotCopyOutcome.Skipped;
        }
        catch (IOException ex) when (IsLockOrVolatileFile(sourceFile, ex))
        {
            // LOCK 文件和运行态临时文件被占用时不应阻断登录态快照。
            stats.AddFailureSample(sourceFile, ex);
            return SnapshotCopyOutcome.Skipped;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            stats.AddFailureSample(sourceFile, ex);
            return SnapshotCopyOutcome.Failed;
        }
    }

    private static bool IsLockOrVolatileFile(string sourceFile, IOException exception)
    {
        var fileName = Path.GetFileName(sourceFile);
        return fileName.Equals("LOCK", StringComparison.OrdinalIgnoreCase) ||
            fileName.Equals("lockfile", StringComparison.OrdinalIgnoreCase) ||
            fileName.EndsWith(".lock", StringComparison.OrdinalIgnoreCase) ||
            exception.Message.Contains("另一个程序", StringComparison.OrdinalIgnoreCase) ||
            exception.Message.Contains("being used by another process", StringComparison.OrdinalIgnoreCase);
    }

    private static int CountSnapshotFiles(string path)
    {
        if (!Directory.Exists(path))
        {
            return 0;
        }

        return Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories).Count();
    }

    private static string FormatFailureSamples(SnapshotCopyStats stats)
    {
        if (stats.FailureSamples.Count == 0)
        {
            return string.Empty;
        }

        return $"，示例：{string.Join("；", stats.FailureSamples)}";
    }

    private static string BuildCaptureSuccessMessage(int roamingFileCount, SnapshotCopyStats roamingStats)
    {
        if (roamingFileCount > 0)
        {
            return $"登录态快照采集完成，auth.json 已保存，Roaming 附加快照 {roamingFileCount} 个文件。";
        }

        return "登录态快照采集完成，auth.json 已保存；" +
            $"Roaming 附加快照为空（源候选 {roamingStats.CandidateFileCount} 个，成功复制 {roamingStats.CopiedFileCount} 个，" +
            $"跳过 {roamingStats.SkippedFileCount} 个，失败 {roamingStats.FailedFileCount} 个" +
            FormatFailureSamples(roamingStats) +
            "），已按 codex-switch 模型继续接管。";
    }

    private sealed class SnapshotCopyStats
    {
        private const int MaxFailureSamples = 3;

        public int CandidateFileCount { get; set; }

        public int CopiedFileCount { get; set; }

        public int SkippedFileCount { get; set; }

        public int FailedFileCount { get; set; }

        public List<string> FailureSamples { get; } = [];

        public void AddFailureSample(string path, Exception exception)
        {
            if (FailureSamples.Count >= MaxFailureSamples)
            {
                return;
            }

            FailureSamples.Add($"{path}: {exception.Message}");
        }
    }

    private enum SnapshotCopyOutcome
    {
        Copied,
        Skipped,
        Failed
    }
}
