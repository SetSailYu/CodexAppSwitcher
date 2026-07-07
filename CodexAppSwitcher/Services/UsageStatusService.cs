using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CodexAppSwitcher.Models;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using Newtonsoft.Json;

namespace CodexAppSwitcher.Services;

/// <summary>
/// Codex 用量状态服务。
/// </summary>
public sealed class UsageStatusService
{
    private static readonly Regex PercentRegex = new(@"(\d{1,3})\s*%\s*(?:剩余|remaining)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex ResetRegex = new(@"重置时间[:：]\s*([^\r\n]+)|reset(?:s)?\s*(?:at|time)?[:：]?\s*([^\r\n]+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly UsageOptions _options;

    /// <summary>
    /// 创建用量状态服务。
    /// </summary>
    public UsageStatusService(UsageOptions options)
    {
        _options = options;
    }

    /// <summary>
    /// 使用账号独立 WebView2 profile 查询 Codex analytics 页面。
    /// </summary>
    public async Task<UsageRefreshResult> RefreshAsync(string browserProfilePath)
    {
        if (string.IsNullOrWhiteSpace(browserProfilePath))
        {
            return UsageRefreshResult.Failure("账号缺少 WebView2 profile 路径。");
        }

        Directory.CreateDirectory(browserProfilePath);
        var window = new Window
        {
            Width = 960,
            Height = 720,
            WindowStyle = WindowStyle.None,
            ShowInTaskbar = false,
            Opacity = 0,
            Left = -12000,
            Top = -12000
        };
        var webView = new WebView2();
        window.Content = webView;

        try
        {
            window.Show();
            var environment = await CoreWebView2Environment.CreateAsync(null, browserProfilePath);
            await webView.EnsureCoreWebView2Async(environment);

            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(Math.Max(5, _options.PageLoadTimeoutSeconds)));
            var loaded = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            webView.CoreWebView2.NavigationCompleted += (_, _) => loaded.TrySetResult();
            webView.CoreWebView2.Navigate(_options.AnalyticsUrl);
            using (timeout.Token.Register(() => loaded.TrySetCanceled(timeout.Token)))
            {
                await loaded.Task;
            }

            await Task.Delay(1500, timeout.Token);
            var text = await ReadVisibleText(webView);
            var snapshot = TryParseVisibleText(text);
            return snapshot is null
                ? UsageRefreshResult.Failure("未从 analytics 页面解析到用量数据。")
                : UsageRefreshResult.Success(snapshot);
        }
        catch (OperationCanceledException)
        {
            return UsageRefreshResult.Failure("analytics 页面加载超时。");
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or UnauthorizedAccessException or COMException)
        {
            return UsageRefreshResult.Failure($"用量刷新失败：{ex.Message}");
        }
        finally
        {
            webView.Dispose();
            window.Close();
        }
    }

    private static async Task<string> ReadVisibleText(WebView2 webView)
    {
        const string script = """
            (() => document.body ? document.body.innerText : '')();
            """;
        var raw = await webView.CoreWebView2.ExecuteScriptAsync(script);
        return JsonConvert.DeserializeObject<string>(raw) ?? string.Empty;
    }

    /// <summary>
    /// 从 analytics 页面可见文本中解析用量快照。
    /// </summary>
    public static UsageSnapshot? TryParseVisibleText(string text)
    {
        var percentMatches = PercentRegex.Matches(text);
        if (percentMatches.Count < 2)
        {
            return null;
        }

        var fiveHourPercent = ClampPercent(percentMatches[0].Groups[1].Value);
        var weeklyPercent = ClampPercent(percentMatches[1].Groups[1].Value);
        var resetMatches = ResetRegex.Matches(text);
        return new UsageSnapshot
        {
            FiveHourRemainingPercent = fiveHourPercent,
            WeeklyRemainingPercent = weeklyPercent,
            FiveHourResetText = GetResetText(resetMatches, 0),
            WeeklyResetText = GetResetText(resetMatches, 1),
            ExtraQuotaText = ExtractExtraQuota(text),
            RefreshedAt = DateTimeOffset.Now
        };
    }

    private static int ClampPercent(string value)
    {
        return int.TryParse(value, out var number) ? Math.Clamp(number, 0, 100) : 0;
    }

    private static string GetResetText(MatchCollection matches, int index)
    {
        if (matches.Count <= index)
        {
            return "未知";
        }

        var match = matches[index];
        return FirstNonEmpty(match.Groups[1].Value, match.Groups[2].Value, "未知").Trim();
    }

    private static string ExtractExtraQuota(string text)
    {
        var match = Regex.Match(text, @"剩余额度\s*[\r\n ]+(\d+)|extra\s+quota\s*[\r\n :]+(\d+)", RegexOptions.IgnoreCase);
        return match.Success ? FirstNonEmpty(match.Groups[1].Value, match.Groups[2].Value, "未知") : "未知";
    }

    private static string FirstNonEmpty(params string[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return string.Empty;
    }
}

/// <summary>
/// 用量刷新结果。
/// </summary>
public sealed class UsageRefreshResult
{
    private UsageRefreshResult(bool isSuccess, string message, UsageSnapshot? snapshot)
    {
        IsSuccess = isSuccess;
        Message = message;
        Snapshot = snapshot;
    }

    /// <summary>
    /// 是否成功。
    /// </summary>
    public bool IsSuccess { get; }

    /// <summary>
    /// 结果消息。
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// 用量快照。
    /// </summary>
    public UsageSnapshot? Snapshot { get; }

    /// <summary>
    /// 创建成功结果。
    /// </summary>
    public static UsageRefreshResult Success(UsageSnapshot snapshot) => new(true, "用量刷新完成。", snapshot);

    /// <summary>
    /// 创建失败结果。
    /// </summary>
    public static UsageRefreshResult Failure(string message) => new(false, message, null);
}
