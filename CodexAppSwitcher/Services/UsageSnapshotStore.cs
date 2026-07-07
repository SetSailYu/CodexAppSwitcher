using System.Collections.Generic;
using System.IO;
using CodexAppSwitcher.Models;
using Newtonsoft.Json;

namespace CodexAppSwitcher.Services;

/// <summary>
/// Codex 用量快照本地存储。
/// </summary>
public sealed class UsageSnapshotStore
{
    private readonly CodexPathResolver _pathResolver;

    /// <summary>
    /// 创建用量快照存储。
    /// </summary>
    public UsageSnapshotStore(CodexPathResolver pathResolver)
    {
        _pathResolver = pathResolver;
    }

    /// <summary>
    /// 保存账号用量快照。
    /// </summary>
    public void Save(string accountId, UsageSnapshot snapshot)
    {
        var accountRoot = GetAccountRoot(accountId);
        Directory.CreateDirectory(accountRoot);

        var json = JsonConvert.SerializeObject(snapshot, Formatting.Indented);
        File.WriteAllText(Path.Combine(accountRoot, "usage.json"), json);
    }

    /// <summary>
    /// 读取全部账号用量快照。
    /// </summary>
    public IReadOnlyDictionary<string, UsageSnapshot> LoadAll()
    {
        var accountsRoot = Path.Combine(_pathResolver.SwitcherDataRoot, "accounts");
        if (!Directory.Exists(accountsRoot))
        {
            return new Dictionary<string, UsageSnapshot>();
        }

        var snapshots = new Dictionary<string, UsageSnapshot>();
        foreach (var accountDirectory in Directory.GetDirectories(accountsRoot))
        {
            var usageFile = Path.Combine(accountDirectory, "usage.json");
            if (!File.Exists(usageFile))
            {
                continue;
            }

            var content = File.ReadAllText(usageFile);
            var snapshot = JsonConvert.DeserializeObject<UsageSnapshot>(content);
            if (snapshot is not null)
            {
                snapshots[Path.GetFileName(accountDirectory)] = snapshot;
            }
        }

        return snapshots;
    }

    private string GetAccountRoot(string accountId) =>
        Path.Combine(_pathResolver.SwitcherDataRoot, "accounts", accountId);
}
