using System;
using System.Collections.Generic;
using System.IO;
using CodexAppSwitcher.Models;
using Newtonsoft.Json;

namespace CodexAppSwitcher.Services;

/// <summary>
/// 账号元数据存储服务。
/// </summary>
public sealed class AccountMetadataStore
{
    private readonly CodexPathResolver _pathResolver;

    /// <summary>
    /// 创建账号元数据存储。
    /// </summary>
    public AccountMetadataStore(CodexPathResolver pathResolver)
    {
        _pathResolver = pathResolver;
    }

    /// <summary>
    /// 保存账号元数据。
    /// </summary>
    public void Save(AccountMetadata account)
    {
        var accountRoot = _pathResolver.GetAccountRootPath(account.AccountId);
        Directory.CreateDirectory(accountRoot);

        var json = JsonConvert.SerializeObject(account, Formatting.Indented);
        File.WriteAllText(Path.Combine(accountRoot, "metadata.json"), json);
    }

    /// <summary>
    /// 删除账号本地数据目录。
    /// </summary>
    public OperationResult Delete(string accountId)
    {
        var accountRoot = _pathResolver.GetAccountRootPath(accountId);
        if (!Directory.Exists(accountRoot))
        {
            return OperationResult.Success("账号本地数据已不存在。");
        }

        try
        {
            Directory.Delete(accountRoot, recursive: true);
            return OperationResult.Success("账号本地数据已删除。");
        }
        catch (IOException ex)
        {
            return OperationResult.Failure($"删除账号本地数据失败：{ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            return OperationResult.Failure($"删除账号本地数据失败：{ex.Message}");
        }
    }

    /// <summary>
    /// 读取全部账号元数据。
    /// </summary>
    public IReadOnlyList<AccountMetadata> LoadAll()
    {
        var accountsRoot = Path.Combine(_pathResolver.SwitcherDataRoot, "accounts");
        if (!Directory.Exists(accountsRoot))
        {
            return [];
        }

        var accounts = new List<AccountMetadata>();
        foreach (var accountDirectory in Directory.GetDirectories(accountsRoot))
        {
            var metadataFile = Path.Combine(accountDirectory, "metadata.json");
            if (!File.Exists(metadataFile))
            {
                continue;
            }

            var content = File.ReadAllText(metadataFile);
            var account = JsonConvert.DeserializeObject<AccountMetadata>(content);
            if (account is not null)
            {
                accounts.Add(account);
            }
        }

        return accounts;
    }
}
