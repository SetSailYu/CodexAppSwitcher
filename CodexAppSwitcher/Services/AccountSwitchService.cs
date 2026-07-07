using System;
using System.IO;
using System.Security.Cryptography;
using CodexAppSwitcher.Models;

namespace CodexAppSwitcher.Services;

/// <summary>
/// 账号登录态切换服务。
/// </summary>
public sealed class AccountSwitchService
{
    private readonly AccountSnapshotService _snapshotService;
    private readonly CodexPathResolver _pathResolver;
    /// <summary>
    /// 创建账号切换服务。
    /// </summary>
    public AccountSwitchService(
        AccountSnapshotService snapshotService,
        CodexPathResolver pathResolver)
    {
        _snapshotService = snapshotService;
        _pathResolver = pathResolver;
    }

    /// <summary>
    /// 执行账号切换。直接替换 live auth.json；调用方负责先关闭 Codex App。
    /// </summary>
    public OperationResult ExecuteSwitch(string sourceAccountId, string targetAccountId)
    {
        var readiness = ValidateSwitchReadiness(sourceAccountId, targetAccountId);
        if (!readiness.IsSuccess)
        {
            return readiness;
        }

        try
        {
            ApplyTargetLoginState(targetAccountId);
            return OperationResult.Success($"账号 auth 已写入，等待 Codex App 重载或重启后读取。{BuildSwitchDiagnostics(targetAccountId)}");
        }
        catch (IOException ex)
        {
            return OperationResult.Failure($"账号切换写入失败：{ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            return OperationResult.Failure($"账号切换写入失败：权限不足。{ex.Message}");
        }
    }

    private OperationResult ValidateSwitchReadiness(string sourceAccountId, string targetAccountId)
    {
        if (string.IsNullOrWhiteSpace(targetAccountId))
        {
            return OperationResult.Failure("未选择目标账号。");
        }

        if (sourceAccountId == targetAccountId)
        {
            return OperationResult.Failure("目标账号已是当前账号。");
        }

        var snapshotRoot = _snapshotService.BuildSnapshotTargetPath(targetAccountId);
        var authSnapshot = Path.Combine(snapshotRoot, "auth.json");
        if (!File.Exists(authSnapshot))
        {
            return OperationResult.Failure("目标账号缺少 Codex auth.json 快照，请重新接管该账号。");
        }

        return OperationResult.Success("切换条件检查通过。");
    }

    private void ApplyTargetLoginState(string targetAccountId)
    {
        var snapshotRoot = _snapshotService.BuildSnapshotTargetPath(targetAccountId);
        var authSnapshot = Path.Combine(snapshotRoot, "auth.json");
        Directory.CreateDirectory(Path.GetDirectoryName(_pathResolver.AuthJsonPath)!);
        CopyFileAtomic(authSnapshot, _pathResolver.AuthJsonPath);
    }

    private string BuildSwitchDiagnostics(string targetAccountId)
    {
        var snapshotRoot = _snapshotService.BuildSnapshotTargetPath(targetAccountId);
        var authText = $"；auth={FilesEqual(Path.Combine(snapshotRoot, "auth.json"), _pathResolver.AuthJsonPath)}";
        return $" auth路径：{_pathResolver.AuthJsonPath}{authText}；仅写 live auth.json。";
    }

    private static void CopyFileAtomic(string sourcePath, string targetPath)
    {
        if (!File.Exists(sourcePath))
        {
            throw new FileNotFoundException("源 auth.json 不存在。", sourcePath);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
        var tempPath = $"{targetPath}.tmp";
        File.Copy(sourcePath, tempPath, overwrite: true);
        File.Move(tempPath, targetPath, overwrite: true);
    }

    private static bool FilesEqual(string sourcePath, string targetPath)
    {
        if (!File.Exists(sourcePath) || !File.Exists(targetPath))
        {
            return false;
        }

        var sourceInfo = new FileInfo(sourcePath);
        var targetInfo = new FileInfo(targetPath);
        return sourceInfo.Length == targetInfo.Length &&
            string.Equals(HashFile(sourcePath), HashFile(targetPath), StringComparison.Ordinal);
    }

    private static string HashFile(string path)
    {
        using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Convert.ToHexString(SHA256.HashData(stream));
    }
}
