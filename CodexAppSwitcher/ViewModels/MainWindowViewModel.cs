using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using CodexAppSwitcher.Infrastructure;
using CodexAppSwitcher.Models;
using CodexAppSwitcher.Services;

namespace CodexAppSwitcher.ViewModels;

/// <summary>
/// 主窗口展示模型。
/// </summary>
public sealed class MainWindowViewModel : INotifyPropertyChanged
{
    private readonly AccountSnapshotService _snapshotService;
    private readonly AccountMetadataStore _metadataStore;
    private readonly CodexProcessService _processService;
    private readonly AccountSwitchService _switchService;
    private readonly AccountIdentityService _identityService;
    private readonly UsageStatusService _usageService;
    private readonly UsageSnapshotStore _usageStore;
    private readonly CodexPathResolver _pathResolver;
    private readonly FeatureFlagOptions _featureFlags;
    private readonly Dictionary<string, UsageSnapshot> _usageSnapshots = [];
    private string _currentAccountId = "current-runtime";
    private string _lastOperation = "已接入配置加载；快照采集默认不覆盖既有账号。";
    private string _codexAppStatus = "等待检测。";
    private string _currentAccountName = "未检测";
    private string _currentAccountStatus = "未检测";
    private string _currentAccountLoginTimeText = "登录时间：等待检测";
    private AccountRowViewModel? _currentAccountUsage;
    private UsageWidgetWindow? _usageWidgetWindow;
    private bool _isUsageRefreshing;
    private bool _isCodexAppCapturing;
    private bool _isCodexAppRunning;
    private bool _isAccountSwitching;
    private bool _isUsageWidgetVisible;
    private string? _capturingAccountId;
    private string? _switchingAccountId;
    private string? _takeoverAccountId;

    /// <summary>
    /// 创建主窗口展示模型。
    /// </summary>
    public MainWindowViewModel()
    {
        var configuration = AppConfigurationLoader.Load();
        var pathResolver = new CodexPathResolver(configuration.CodexPaths);
        _pathResolver = pathResolver;
        _featureFlags = configuration.FeatureFlags;
        _processService = new CodexProcessService();
        _snapshotService = new AccountSnapshotService(pathResolver);
        _metadataStore = new AccountMetadataStore(pathResolver);
        _identityService = new AccountIdentityService(pathResolver);
        _usageService = new UsageStatusService(configuration.Usage);
        _usageStore = new UsageSnapshotStore(pathResolver);
        _switchService = new AccountSwitchService(
            _snapshotService,
            pathResolver);
        AddChatGptAccountCommand = new RelayCommand(AddChatGptAccount);
        RefreshUsageCommand = new RelayCommand(() => _ = RefreshUsageAsync());
        ToggleUsageWidgetCommand = new RelayCommand(ToggleUsageWidget);
        OpenCodexAppCommand = new RelayCommand(() => _ = OpenCodexAppAsync());
        StopCodexAppCommand = new RelayCommand(() => _ = StopCodexAppAsync());
        TakeoverCodexAppCommand = new RelayCommand(parameter => _ = TakeoverCodexAppAsync(parameter));
        SwitchAccountCommand = new RelayCommand(parameter => _ = SwitchAccountAsync(parameter));
        ClearOperationLogsCommand = new RelayCommand(ClearOperationLogs);
        OpenDataDirectoryCommand = new RelayCommand(OpenDataDirectory);
        RenameAccountCommand = new RelayCommand(RenameAccount);
        OpenAccountSnapshotDirectoryCommand = new RelayCommand(OpenAccountSnapshotDirectory);
        OpenAccountWebProfileDirectoryCommand = new RelayCommand(OpenAccountWebProfileDirectory);
        DeleteAccountCommand = new RelayCommand(DeleteAccount);
        RefreshCodexAppStatus();

        var accounts = _metadataStore.LoadAll();
        var currentAccount = accounts.FirstOrDefault(account => account.IsCurrent);
        _currentAccountId = currentAccount?.AccountId ?? accounts.FirstOrDefault()?.AccountId ?? _currentAccountId;
        SetCurrentAccountDisplay(currentAccount);
        foreach (var usage in _usageStore.LoadAll())
        {
            _usageSnapshots[usage.Key] = usage.Value;
        }

        foreach (var account in accounts)
        {
            _usageSnapshots.TryGetValue(account.AccountId, out var usage);
            Accounts.Add(CreateAccountRow(account, usage));
        }

        UpdateCurrentAccountUsageDisplay();
        BuildSafetyChecks(pathResolver);
        AddOperationLog("信息", $"程序启动完成，已加载 {accounts.Count} 个账号。");
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// 账号展示列表。
    /// </summary>
    public ObservableCollection<AccountRowViewModel> Accounts { get; } = [];

    /// <summary>
    /// 操作日志行。
    /// </summary>
    public ObservableCollection<OperationLogRow> OperationLogs { get; } = [];

    /// <summary>
    /// 安全检查展示项。
    /// </summary>
    public ObservableCollection<SafetyCheckRow> SafetyChecks { get; } = [];

    /// <summary>
    /// 账号列表标题。
    /// </summary>
    public string AccountListTitle => $"账号列表（{Accounts.Count}）";

    /// <summary>
    /// 是否已有账号。
    /// </summary>
    public bool HasAccounts => Accounts.Count > 0;

    /// <summary>
    /// 是否已启用真实用量刷新。
    /// </summary>
    public bool IsUsageRefreshEnabled => _featureFlags.EnableUsageRefresh;

    /// <summary>
    /// 刷新用量按钮提示。
    /// </summary>
    public string RefreshUsageTooltip => IsUsageRefreshEnabled
        ? "使用各账号的 WebView2 登录态刷新 Codex analytics 用量。"
        : "真实用量刷新已关闭。开启 EnableUsageRefresh 后重启工具。";

    /// <summary>
    /// 是否正在刷新用量。
    /// </summary>
    public bool IsUsageRefreshing
    {
        get => _isUsageRefreshing;
        private set
        {
            _isUsageRefreshing = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// 是否正在接管或更新 Codex App 登录态。
    /// </summary>
    public bool IsCodexAppCapturing
    {
        get => _isCodexAppCapturing;
        private set
        {
            _isCodexAppCapturing = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// 是否正在执行账号切换。
    /// </summary>
    public bool IsAccountSwitching
    {
        get => _isAccountSwitching;
        private set
        {
            _isAccountSwitching = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Codex App 是否正在运行。
    /// </summary>
    public bool IsCodexAppRunning
    {
        get => _isCodexAppRunning;
        private set
        {
            _isCodexAppRunning = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CodexAppRunStateText));
        }
    }

    /// <summary>
    /// Codex App 运行状态标签。
    /// </summary>
    public string CodexAppRunStateText => IsCodexAppRunning ? "运行中" : "未运行";

    /// <summary>
    /// 额度挂件是否正在显示。
    /// </summary>
    public bool IsUsageWidgetVisible
    {
        get => _isUsageWidgetVisible;
        private set
        {
            _isUsageWidgetVisible = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(UsageWidgetButtonText));
        }
    }

    /// <summary>
    /// 额度挂件按钮文本。
    /// </summary>
    public string UsageWidgetButtonText => IsUsageWidgetVisible ? "隐藏挂件" : "额度挂件";

    /// <summary>
    /// 添加 ChatGPT Web 账号命令。
    /// </summary>
    public ICommand AddChatGptAccountCommand { get; }

    /// <summary>
    /// 刷新 Codex 用量命令。
    /// </summary>
    public ICommand RefreshUsageCommand { get; }

    /// <summary>
    /// 显示或隐藏当前账号额度挂件命令。
    /// </summary>
    public ICommand ToggleUsageWidgetCommand { get; }

    /// <summary>
    /// 启动 Codex App 命令。
    /// </summary>
    public ICommand OpenCodexAppCommand { get; }

    /// <summary>
    /// 停止 Codex App 命令。
    /// </summary>
    public ICommand StopCodexAppCommand { get; }

    /// <summary>
    /// 将当前 Codex App 登录态接管到指定账号。
    /// </summary>
    public ICommand TakeoverCodexAppCommand { get; }

    /// <summary>
    /// 账号切换命令。
    /// </summary>
    public ICommand SwitchAccountCommand { get; }

    /// <summary>
    /// 清空操作日志命令。
    /// </summary>
    public ICommand ClearOperationLogsCommand { get; }

    /// <summary>
    /// 打开数据目录命令。
    /// </summary>
    public ICommand OpenDataDirectoryCommand { get; }

    /// <summary>
    /// 重命名账号命令。
    /// </summary>
    public ICommand RenameAccountCommand { get; }

    /// <summary>
    /// 打开账号 Codex App 快照目录命令。
    /// </summary>
    public ICommand OpenAccountSnapshotDirectoryCommand { get; }

    /// <summary>
    /// 打开账号 Web 登录目录命令。
    /// </summary>
    public ICommand OpenAccountWebProfileDirectoryCommand { get; }

    /// <summary>
    /// 删除账号本地数据命令。
    /// </summary>
    public ICommand DeleteAccountCommand { get; }

    /// <summary>
    /// Codex App 当前状态。
    /// </summary>
    public string CodexAppStatus
    {
        get => _codexAppStatus;
        private set
        {
            _codexAppStatus = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// 当前活动账号名称。
    /// </summary>
    public string CurrentAccountName
    {
        get => _currentAccountName;
        private set
        {
            _currentAccountName = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// 当前账号状态。
    /// </summary>
    public string CurrentAccountStatus
    {
        get => _currentAccountStatus;
        private set
        {
            _currentAccountStatus = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// 当前账号登录态采集时间文本。
    /// </summary>
    public string CurrentAccountLoginTimeText
    {
        get => _currentAccountLoginTimeText;
        private set
        {
            _currentAccountLoginTimeText = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// 当前账号用量悬浮展示数据。
    /// </summary>
    public AccountRowViewModel? CurrentAccountUsage
    {
        get => _currentAccountUsage;
        private set
        {
            _currentAccountUsage = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Switcher 数据目录展示文本。
    /// </summary>
    public string DataDirectoryText => $"数据目录：{_pathResolver.SwitcherDataRoot}";

    /// <summary>
    /// 最近操作摘要。
    /// </summary>
    public string LastOperation
    {
        get => _lastOperation;
        private set
        {
            _lastOperation = value;
            OnPropertyChanged();
        }
    }

    private void AddChatGptAccount()
    {
        var account = new AccountMetadata
        {
            Hint = "已添加 ChatGPT Web 登录态，等待采集 Codex App 登录态。"
        };
        account.BrowserProfilePath = _pathResolver.GetAccountBrowserProfilePath(account.AccountId);

        var webIdentity = ShowAddAccountWindow(account.BrowserProfilePath);
        var fallbackIdentity = webIdentity.HasUserIdentifier ? webIdentity : _identityService.DetectCurrentIdentity();
        if (!fallbackIdentity.HasUserIdentifier)
        {
            LastOperation = "未识别账号 ID，已取消添加账号。";
            AddOperationLog("警告", LastOperation);
            return;
        }

        account.DisplayName = fallbackIdentity.UserIdentifier;
        account.UserIdentifier = fallbackIdentity.UserIdentifier;
        account.Hint = $"ChatGPT Web 登录态已添加；身份来源：{fallbackIdentity.Source}";

        var confirmedBrowserProfilePath = account.BrowserProfilePath;
        var accounts = _metadataStore.LoadAll().ToList();
        var existingAccount = accounts.FirstOrDefault(existing =>
            string.Equals(existing.UserIdentifier, fallbackIdentity.UserIdentifier, System.StringComparison.OrdinalIgnoreCase));
        var allowOverwrite = false;
        if (existingAccount is not null)
        {
            var confirmation = ThemedDialogService.Confirm(
                Application.Current.MainWindow,
                "确认覆盖账号",
                $"账号已存在：{existingAccount.DisplayName}\n\n是否用当前登录状态覆盖该账号的 Web 登录态？\nCodex App 快照不会在添加账号流程中采集，取消则不会新增重复账号。",
                "确认覆盖",
                "取消");
            if (!confirmation)
            {
                LastOperation = "用户取消覆盖既有账号，未新增重复账号。";
                AddOperationLog("信息", LastOperation);
                return;
            }

            account = existingAccount;
            account.BrowserProfilePath = confirmedBrowserProfilePath;
            account.DisplayName = fallbackIdentity.UserIdentifier;
            account.UserIdentifier = fallbackIdentity.UserIdentifier;
            allowOverwrite = true;
        }

        account.Hint = allowOverwrite
            ? $"ChatGPT Web 登录态已更新；身份来源：{fallbackIdentity.Source}"
            : $"ChatGPT Web 登录态已添加；身份来源：{fallbackIdentity.Source}";
        _metadataStore.Save(account);
        RebuildAccountRows(_metadataStore.LoadAll());
        LastOperation = allowOverwrite
            ? "既有账号 Web 登录态已更新；Codex App 快照未变更。"
            : "账号已添加；请通过账号行接管来采集 Codex App 快照。";
        AddOperationLog("成功", LastOperation);
    }

    private void ToggleUsageWidget()
    {
        if (IsUsageWidgetVisible)
        {
            _usageWidgetWindow?.Hide();
            IsUsageWidgetVisible = false;
            return;
        }

        _usageWidgetWindow ??= new UsageWidgetWindow
        {
            DataContext = this
        };
        _usageWidgetWindow.Show();
        _usageWidgetWindow.Activate();
        IsUsageWidgetVisible = true;
    }

    private static AccountIdentitySnapshot ShowAddAccountWindow(string profilePath)
    {
        var window = new AddAccountWindow(profilePath)
        {
            Owner = Application.Current.MainWindow
        };
        var accepted = window.ShowDialog() == true;
        return accepted
            ? new AccountIdentitySnapshot { UserIdentifier = window.UserIdentifier, Source = "WebView2 页面识别" }
            : new AccountIdentitySnapshot();
    }

    private AccountRowViewModel CreateAccountRow(AccountMetadata account, UsageSnapshot? usage)
    {
        var hint = string.IsNullOrWhiteSpace(account.Hint) ? "未填写备注" : account.Hint;
        var healthText = account.LastCapturedAt.HasValue ? "已接管" : "未接管";
        return new AccountRowViewModel(
            account.AccountId,
            account.DisplayName,
            hint,
            healthText,
            usage?.FiveHourRemainingPercent,
            usage?.WeeklyRemainingPercent,
            usage?.FiveHourResetText ?? "等待检测",
            usage?.WeeklyResetText ?? "等待检测",
            account.IsCurrent,
            account.AccountId == _capturingAccountId,
            account.AccountId == _switchingAccountId,
            account.AccountId == _takeoverAccountId);
    }

    private async Task RefreshUsageAsync()
    {
        if (IsUsageRefreshing)
        {
            return;
        }

        IsUsageRefreshing = true;
        try
        {
            LastOperation = "正在刷新账号用量...";
            if (!_featureFlags.EnableUsageRefresh)
            {
                LastOperation = "用量刷新未启用：请在 appsettings.json 中开启 FeatureFlags:EnableUsageRefresh 后重启工具。";
                AddOperationLog("警告", LastOperation);
                return;
            }

            var accounts = _metadataStore.LoadAll();
            if (accounts.Count == 0)
            {
                LastOperation = "暂无可刷新的账号。";
                AddOperationLog("信息", LastOperation);
                return;
            }

            var successCount = 0;
            var failedAccountNames = new List<string>();
            foreach (var account in accounts)
            {
                var result = await _usageService.RefreshAsync(account.BrowserProfilePath);
                if (result.IsSuccess && result.Snapshot is not null)
                {
                    _usageSnapshots[account.AccountId] = result.Snapshot;
                    _usageStore.Save(account.AccountId, result.Snapshot);
                    successCount++;
                    AddOperationLog("成功", $"已刷新账号 {account.DisplayName} 的用量。");
                    continue;
                }

                failedAccountNames.Add(account.DisplayName);
                AddOperationLog("警告", $"账号 {account.DisplayName} 用量刷新失败：{result.Message}");
            }

            RebuildAccountRows(accounts);
            LastOperation = BuildUsageRefreshSummary(accounts.Count, successCount, failedAccountNames);
            AddOperationLog(failedAccountNames.Count == 0 ? "成功" : "警告", LastOperation);
        }
        finally
        {
            IsUsageRefreshing = false;
        }
    }

    private void RebuildAccountRows(IReadOnlyList<AccountMetadata> accounts)
    {
        Accounts.Clear();
        foreach (var account in accounts)
        {
            _usageSnapshots.TryGetValue(account.AccountId, out var usage);
            Accounts.Add(CreateAccountRow(account, usage));
        }

        NotifyAccountListChanged();
        UpdateCurrentAccountUsageDisplay();
    }

    private static string BuildUsageRefreshSummary(int totalCount, int successCount, IReadOnlyCollection<string> failedAccountNames)
    {
        if (totalCount == 0)
        {
            return "暂无可刷新的账号。";
        }

        if (failedAccountNames.Count == 0)
        {
            return $"已刷新 {successCount} 个账号的用量。";
        }

        var failedPreview = string.Join("、", failedAccountNames.Take(3));
        var overflowText = failedAccountNames.Count > 3 ? $" 等 {failedAccountNames.Count} 个账号" : string.Empty;
        return successCount == 0
            ? $"用量刷新失败：{failedPreview}{overflowText} 未刷新，查看操作日志。"
            : $"已刷新 {successCount}/{totalCount} 个账号；{failedPreview}{overflowText} 失败，查看操作日志。";
    }

    private async Task SwitchAccountAsync(object? parameter)
    {
        if (IsAccountSwitching)
        {
            return;
        }

        if (parameter is not AccountRowViewModel targetAccount)
        {
            LastOperation = "未选择目标账号。";
            return;
        }

        var confirmation = ThemedDialogService.Confirm(
            Application.Current.MainWindow,
            "确认账号切换",
            BuildSwitchWarning(targetAccount),
            "继续切换",
            "取消");
        if (!confirmation)
        {
            LastOperation = "用户取消账号切换。";
            AddOperationLog("信息", "用户取消账号切换");
            return;
        }

        IsAccountSwitching = true;
        _switchingAccountId = targetAccount.AccountId;
        RebuildAccountRows(_metadataStore.LoadAll());
        LastOperation = $"正在切换到账号 {targetAccount.DisplayName}...";
        try
        {
            if (_processService.IsCodexRunning())
            {
                var closeResult = await Task.Run(_processService.CloseForSwitch);
                AddOperationLog(closeResult.IsSuccess ? "成功" : "警告", $"关闭 Codex App：{closeResult.Message}");
                RefreshCodexAppStatus();
                if (!closeResult.IsSuccess)
                {
                    LastOperation = $"切换已中止：{closeResult.Message}";
                    return;
                }
            }

            var result = await Task.Run(() => _switchService.ExecuteSwitch(_currentAccountId, targetAccount.AccountId));
            LastOperation = result.Message;
            AddOperationLog(result.IsSuccess ? "成功" : "警告", $"切换执行：{result.Message}");
            if (result.IsSuccess)
            {
                MarkCurrentAccount(targetAccount.AccountId);
                var startResult = await Task.Run(_processService.StartCodexApp);
                LastOperation = startResult.IsSuccess
                    ? $"账号切换完成，{startResult.Message}"
                    : $"账号切换完成，但 Codex App 自动启动失败：{startResult.Message}";
                AddOperationLog(startResult.IsSuccess ? "成功" : "警告", $"启动 Codex App：{startResult.Message}");
                RefreshCodexAppStatus();
            }

        }
        finally
        {
            _switchingAccountId = null;
            IsAccountSwitching = false;
            RebuildAccountRows(_metadataStore.LoadAll());
            RefreshCodexAppStatus();
            RefreshSafetyChecks();
        }
    }

    private async Task OpenCodexAppAsync()
    {
        var result = await Task.Run(_processService.StartCodexApp);
        LastOperation = result.Message;
        AddOperationLog(result.IsSuccess ? "成功" : "警告", $"启动 Codex App：{result.Message}");
        RefreshCodexAppStatus();
        RefreshSafetyChecks();
    }

    private async Task StopCodexAppAsync()
    {
        var result = await Task.Run(_processService.CloseForSwitch);
        LastOperation = result.Message;
        AddOperationLog(result.IsSuccess ? "成功" : "警告", $"停止 Codex App：{result.Message}");
        RefreshCodexAppStatus();
        RefreshSafetyChecks();
    }

    private async Task TakeoverCodexAppAsync(object? parameter)
    {
        if (IsCodexAppCapturing)
        {
            return;
        }

        if (parameter is not AccountRowViewModel targetAccount)
        {
            LastOperation = "未选择要接管的账号。";
            AddOperationLog("警告", LastOperation);
            return;
        }

        var isRefreshCurrentSnapshot = targetAccount.IsManaged && targetAccount.IsCurrent;
        if (!isRefreshCurrentSnapshot && _takeoverAccountId != targetAccount.AccountId)
        {
            StartTakeover(targetAccount);
            return;
        }

        var confirmation = ThemedDialogService.Confirm(
            Application.Current.MainWindow,
            isRefreshCurrentSnapshot ? "更新当前账号快照" : "完成接管账号",
            isRefreshCurrentSnapshot ? BuildRefreshCurrentSnapshotWarning(targetAccount) : BuildCompleteTakeoverWarning(targetAccount),
            isRefreshCurrentSnapshot ? "更新快照" : "完成接管",
            "取消");
        if (!confirmation)
        {
            LastOperation = isRefreshCurrentSnapshot ? "用户取消更新当前账号快照。" : "用户取消完成接管。";
            AddOperationLog("信息", LastOperation);
            return;
        }

        IsCodexAppCapturing = true;
        _capturingAccountId = targetAccount.AccountId;
        RebuildAccountRows(_metadataStore.LoadAll());
        LastOperation = isRefreshCurrentSnapshot
            ? $"正在更新当前账号 {targetAccount.DisplayName} 的 Codex App 快照..."
            : $"正在接管账号 {targetAccount.DisplayName}...";
        var accounts = _metadataStore.LoadAll().ToList();
        try
        {
            var account = accounts.FirstOrDefault(item => item.AccountId == targetAccount.AccountId);
            if (account is null)
            {
                LastOperation = $"接管失败：未找到账号 {targetAccount.DisplayName} 的本地元数据。";
                AddOperationLog("警告", LastOperation);
                return;
            }

            var currentIdentity = _identityService.DetectCurrentIdentity();
            if (currentIdentity.HasUserIdentifier &&
                !string.IsNullOrWhiteSpace(account.UserIdentifier) &&
                !string.Equals(currentIdentity.UserIdentifier, account.UserIdentifier, StringComparison.OrdinalIgnoreCase))
            {
                LastOperation = $"接管失败：当前 Codex App 登录账号是 {currentIdentity.UserIdentifier}，不是目标账号 {account.UserIdentifier}。";
                AddOperationLog("警告", LastOperation);
                return;
            }

            var result = await Task.Run(() => _snapshotService.CaptureCurrentLoginState(account.AccountId, allowOverwrite: true));
            if (!result.IsSuccess)
            {
                LastOperation = $"接管失败：{result.Message}";
                AddOperationLog("警告", LastOperation);
                return;
            }

            foreach (var item in accounts)
            {
                item.IsCurrent = false;
            }

            account.IsCurrent = true;
            account.LastCapturedAt = DateTimeOffset.Now;
            account.Hint = BuildTakeoverAccountHint(account.Hint);
            _currentAccountId = account.AccountId;
            SetCurrentAccountDisplay(account);

            foreach (var item in accounts)
            {
                _metadataStore.Save(item);
            }

            _takeoverAccountId = null;
            LastOperation = isRefreshCurrentSnapshot
                ? $"已更新当前账号 {account.DisplayName} 的 Codex App 快照。"
                : $"已接管账号 {account.DisplayName}。";
            AddOperationLog("成功", LastOperation);
        }
        finally
        {
            _capturingAccountId = null;
            IsCodexAppCapturing = false;
            RebuildAccountRows(_metadataStore.LoadAll());
            RefreshCodexAppStatus();
            RefreshSafetyChecks();
        }
    }

    private string BuildSwitchWarning(AccountRowViewModel targetAccount)
    {
        return $"目标账号：{targetAccount.DisplayName}\n"
            + "操作：直接执行真实账号切换\n\n"
            + "继续后会先关闭全部 Codex App 进程，再把目标账号 auth.json 写入 live auth。"
            + "写入完成后会自动启动 Codex App 读取新账号。是否继续？";
    }

    private void StartTakeover(AccountRowViewModel targetAccount)
    {
        var confirmation = ThemedDialogService.Confirm(
            Application.Current.MainWindow,
            "接管账号",
            BuildStartTakeoverWarning(targetAccount),
            "开始接管",
            "取消");
        if (!confirmation)
        {
            LastOperation = "用户取消接管账号。";
            AddOperationLog("信息", LastOperation);
            return;
        }

        _takeoverAccountId = targetAccount.AccountId;
        LastOperation = $"已进入账号 {targetAccount.DisplayName} 的接管流程。请在 Codex App 登录该账号后，再点击“完成”。";
        AddOperationLog("信息", LastOperation);
        RebuildAccountRows(_metadataStore.LoadAll());
    }

    private string BuildStartTakeoverWarning(AccountRowViewModel targetAccount)
    {
        var actionText = targetAccount.IsManaged ? "重新接管" : "接管";
        return $"目标账号：{targetAccount.DisplayName}\n"
            + $"当前使用账号：{CurrentAccountName}\n\n"
            + $"本步骤不会立即把当前 Codex App 登录态写入目标账号，也不会修改当前已登录账号快照。\n"
            + $"开始{actionText}后，请你手动让 Codex App 登录目标账号；登录完成后回到本工具点击“完成”。";
    }

    private static string BuildCompleteTakeoverWarning(AccountRowViewModel targetAccount)
    {
        return $"目标账号：{targetAccount.DisplayName}\n\n"
            + "请确认当前 Codex App 已经登录这个目标账号。\n"
            + "确认后工具会采集当前 Codex App 登录态作为该账号快照；如果当前 App 不是该账号，会造成错误接管。";
    }

    private static string BuildRefreshCurrentSnapshotWarning(AccountRowViewModel targetAccount)
    {
        return $"当前账号：{targetAccount.DisplayName}\n\n"
            + "请确认当前 Codex App 正在使用这个账号。\n"
            + "确认后工具会用当前本机 Codex App 登录态覆盖该账号已有快照，不会修改其它账号快照。";
    }

    private static string BuildTakeoverAccountHint(string existingHint)
    {
        const string takeoverHint = "已接管 Codex App 登录态。";
        if (string.IsNullOrWhiteSpace(existingHint))
        {
            return takeoverHint;
        }

        return existingHint.Contains(takeoverHint, StringComparison.Ordinal)
            ? existingHint
            : $"{existingHint} {takeoverHint}";
    }

    private void MarkCurrentAccount(string accountId)
    {
        var accounts = _metadataStore.LoadAll().ToList();
        var currentAccount = accounts.FirstOrDefault(account => account.AccountId == accountId);
        if (currentAccount is null)
        {
            return;
        }

        foreach (var account in accounts)
        {
            account.IsCurrent = account.AccountId == accountId;
            _metadataStore.Save(account);
        }

        _currentAccountId = currentAccount.AccountId;
        SetCurrentAccountDisplay(currentAccount);
    }

    private void ClearOperationLogs()
    {
        OperationLogs.Clear();
        LastOperation = "操作日志已清空。";
    }

    private void OpenDataDirectory()
    {
        OpenDirectory(_pathResolver.SwitcherDataRoot, $"已打开数据目录：{_pathResolver.SwitcherDataRoot}", createIfMissing: true);
    }

    private void RenameAccount(object? parameter)
    {
        if (parameter is not AccountRowViewModel row)
        {
            LastOperation = "未选择要重命名的账号。";
            AddOperationLog("警告", LastOperation);
            return;
        }

        var accounts = _metadataStore.LoadAll().ToList();
        var account = accounts.FirstOrDefault(item => item.AccountId == row.AccountId);
        if (account is null)
        {
            LastOperation = $"重命名失败：未找到账号 {row.DisplayName} 的本地元数据。";
            AddOperationLog("警告", LastOperation);
            return;
        }

        var newName = ThemedDialogService.Prompt(
            Application.Current.MainWindow,
            "重命名账号",
            $"当前账号：{account.DisplayName}",
            account.DisplayName,
            "保存",
            "取消");
        if (newName is null)
        {
            LastOperation = "用户取消重命名账号。";
            AddOperationLog("信息", LastOperation);
            return;
        }

        var normalizedName = newName.Trim();
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            LastOperation = "重命名失败：账号名称不能为空。";
            AddOperationLog("警告", LastOperation);
            return;
        }

        if (string.Equals(account.DisplayName, normalizedName, StringComparison.Ordinal))
        {
            LastOperation = "账号名称未变化。";
            AddOperationLog("信息", LastOperation);
            return;
        }

        account.DisplayName = normalizedName;
        _metadataStore.Save(account);
        if (account.IsCurrent || string.Equals(_currentAccountId, account.AccountId, StringComparison.Ordinal))
        {
            SetCurrentAccountDisplay(account);
        }

        RebuildAccountRows(_metadataStore.LoadAll());
        LastOperation = $"已重命名账号为 {normalizedName}。";
        AddOperationLog("成功", LastOperation);
    }

    private void OpenAccountSnapshotDirectory(object? parameter)
    {
        if (parameter is not AccountRowViewModel row)
        {
            LastOperation = "未选择要打开目录的账号。";
            AddOperationLog("警告", LastOperation);
            return;
        }

        var path = _pathResolver.GetAccountLoginStatePath(row.AccountId);
        OpenDirectory(path, $"已打开账号 {row.DisplayName} 的 Codex App 快照目录：{path}", createIfMissing: false);
    }

    private void OpenAccountWebProfileDirectory(object? parameter)
    {
        if (parameter is not AccountRowViewModel row)
        {
            LastOperation = "未选择要打开目录的账号。";
            AddOperationLog("警告", LastOperation);
            return;
        }

        var path = _pathResolver.GetAccountBrowserProfilePath(row.AccountId);
        OpenDirectory(path, $"已打开账号 {row.DisplayName} 的 Web 登录目录：{path}", createIfMissing: false);
    }

    private void DeleteAccount(object? parameter)
    {
        if (parameter is not AccountRowViewModel row)
        {
            LastOperation = "未选择要删除的账号。";
            AddOperationLog("警告", LastOperation);
            return;
        }

        if (IsAccountSwitching || IsCodexAppCapturing)
        {
            LastOperation = "当前正在切换或接管账号，暂不能删除账号。";
            AddOperationLog("警告", LastOperation);
            return;
        }

        if (row.IsCurrent || string.Equals(_currentAccountId, row.AccountId, StringComparison.Ordinal))
        {
            LastOperation = "当前使用账号不能删除，请先切换到其他账号。";
            AddOperationLog("警告", LastOperation);
            return;
        }

        var confirmation = ThemedDialogService.Confirm(
            Application.Current.MainWindow,
            "删除账号",
            $"将删除账号 {row.DisplayName} 的本地元数据、Web 登录目录和 Codex App 快照目录。\n\n不会删除线上账号，但本地删除后不可恢复。是否继续？",
            "确认删除",
            "取消");
        if (!confirmation)
        {
            LastOperation = "用户取消删除账号。";
            AddOperationLog("信息", LastOperation);
            return;
        }

        var result = _metadataStore.Delete(row.AccountId);
        LastOperation = result.Message;
        AddOperationLog(result.IsSuccess ? "成功" : "警告", $"删除账号 {row.DisplayName}：{result.Message}");
        RebuildAccountRows(_metadataStore.LoadAll());
        RefreshSafetyChecks();
    }

    private void OpenDirectory(string path, string successMessage, bool createIfMissing)
    {
        try
        {
            if (createIfMissing)
            {
                Directory.CreateDirectory(path);
            }
            else if (!Directory.Exists(path))
            {
                LastOperation = $"目录不存在：{path}";
                AddOperationLog("警告", LastOperation);
                return;
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            });
            LastOperation = successMessage;
            AddOperationLog("成功", LastOperation);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or Win32Exception or InvalidOperationException)
        {
            LastOperation = $"打开目录失败：{ex.Message}";
            AddOperationLog("警告", LastOperation);
        }
    }

    private void SetCurrentAccountDisplay(AccountMetadata? account)
    {
        CurrentAccountName = account?.DisplayName ?? "未检测";
        CurrentAccountStatus = account is null ? "未检测" : "当前使用";
        CurrentAccountLoginTimeText = account?.LastCapturedAt is null
            ? "登录时间：等待检测"
            : $"登录时间：{account.LastCapturedAt.Value.LocalDateTime:yyyy-MM-dd HH:mm}";
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private void NotifyAccountListChanged()
    {
        OnPropertyChanged(nameof(AccountListTitle));
        OnPropertyChanged(nameof(HasAccounts));
    }

    private void UpdateCurrentAccountUsageDisplay()
    {
        CurrentAccountUsage = Accounts.FirstOrDefault(account => account.IsCurrent)
            ?? Accounts.FirstOrDefault(account => account.AccountId == _currentAccountId);
    }

    private void RefreshCodexAppStatus()
    {
        IsCodexAppRunning = _processService.IsCodexWindowOpen();
        CodexAppStatus = _processService.GetProcessSummary();
    }

    private void BuildSafetyChecks(CodexPathResolver pathResolver)
    {
        var authDirectory = Path.GetDirectoryName(pathResolver.AuthJsonPath);
        SafetyChecks.Add(new SafetyCheckRow("配置已加载", true));
        SafetyChecks.Add(new SafetyCheckRow($"live auth：{(File.Exists(pathResolver.AuthJsonPath) ? "存在" : "未找到")}", File.Exists(pathResolver.AuthJsonPath)));
        SafetyChecks.Add(new SafetyCheckRow($"auth 目录：{authDirectory ?? "未解析"}", authDirectory is not null && Directory.Exists(authDirectory)));
        SafetyChecks.Add(new SafetyCheckRow($"Roaming 附加目录：{(Directory.Exists(pathResolver.RoamingCodexPath) ? "存在" : "未找到")}", Directory.Exists(pathResolver.RoamingCodexPath)));
    }

    private void RefreshSafetyChecks()
    {
        SafetyChecks.Clear();
        BuildSafetyChecks(_pathResolver);
    }

    private void AddOperationLog(string status, string message)
    {
        OperationLogs.Add(new OperationLogRow(System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), status, message));
    }
}

/// <summary>
/// 操作日志展示行。
/// </summary>
public sealed class OperationLogRow
{
    /// <summary>
    /// 创建操作日志行。
    /// </summary>
    public OperationLogRow(string timeText, string status, string message)
    {
        TimeText = timeText;
        Status = status;
        Message = message;
    }

    /// <summary>
    /// 时间文本。
    /// </summary>
    public string TimeText { get; }

    /// <summary>
    /// 状态文本。
    /// </summary>
    public string Status { get; }

    /// <summary>
    /// 消息文本。
    /// </summary>
    public string Message { get; }
}

/// <summary>
/// 安全检查展示行。
/// </summary>
public sealed class SafetyCheckRow
{
    /// <summary>
    /// 创建安全检查展示行。
    /// </summary>
    public SafetyCheckRow(string text, bool isPassed)
    {
        Text = text;
        IsPassed = isPassed;
        StatusText = isPassed ? "通过" : "需处理";
    }

    /// <summary>
    /// 检查文本。
    /// </summary>
    public string Text { get; }

    /// <summary>
    /// 是否通过。
    /// </summary>
    public bool IsPassed { get; }

    /// <summary>
    /// 状态文本。
    /// </summary>
    public string StatusText { get; }
}
