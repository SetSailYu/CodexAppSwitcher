using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CodexAppSwitcher.Models;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CodexAppSwitcher.Services;

/// <summary>
/// Codex 用量状态服务。
/// </summary>
public sealed class UsageStatusService
{
    private const string UsageApiPath = "/backend-api/wham/usage";
    private const string CodexHomeUrl = "https://chatgpt.com/codex";
    private static readonly Regex PercentRegex = new(
        @"(?:剩余|remaining)\s*(\d{1,3})\s*%|(\d{1,3})\s*%\s*(?:剩余|remaining)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex ResetRegex = new(
        @"重置时间[:：]\s*([^\r\n]+)|将于\s*([^\r\n]+?)\s*重置|reset(?:s)?\s*(?:at|time)?[:：]?\s*([^\r\n]+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly UsageOptions _options;

    /// <summary>
    /// 创建用量状态服务。
    /// </summary>
    public UsageStatusService(UsageOptions options)
    {
        _options = options;
    }

    /// <summary>
    /// 使用账号独立 WebView2 profile 查询 Codex 用量 API。
    /// </summary>
    public async Task<UsageRefreshResult> RefreshAsync(string browserProfilePath, string? accountDisplayName = null)
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
            var apiResponse = await CaptureWhamUsageFromAnalyticsAsync(webView, _options.AnalyticsUrl, timeout.Token);
            if (apiResponse is null || apiResponse.StatusCode < 200 || apiResponse.StatusCode >= 300)
            {
                apiResponse = await FetchWhamUsageJson(webView);
            }

            if (apiResponse.StatusCode < 200 || apiResponse.StatusCode >= 300)
            {
                return UsageRefreshResult.Failure(BuildUsageApiHttpError(apiResponse.StatusCode, string.Empty, apiResponse.Body));
            }

            var snapshot = TryParseUsageApiJson(apiResponse.Body);
            return snapshot is null
                ? UsageRefreshResult.Failure(BuildUsageApiParseError(apiResponse.Body))
                : UsageRefreshResult.Success(snapshot, $"用量刷新完成（{apiResponse.Source}，HTTP {apiResponse.StatusCode}）。");
        }
        catch (OperationCanceledException)
        {
            return UsageRefreshResult.Failure("wham usage 接口等待超时。");
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

    private static async Task<UsageApiResponse?> CaptureWhamUsageFromAnalyticsAsync(
        WebView2 webView,
        string url,
        CancellationToken cancellationToken)
    {
        var apiResponse = new TaskCompletionSource<UsageApiResponse?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var loaded = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs args) => loaded.TrySetResult();
        async void OnResponseReceived(object? sender, CoreWebView2DevToolsProtocolEventReceivedEventArgs args)
        {
            try
            {
                var payload = JObject.Parse(args.ParameterObjectAsJson);
                var response = payload["response"];
                var responseUrl = response?.Value<string>("url") ?? string.Empty;
                if (!IsUsageApiUrl(responseUrl))
                {
                    return;
                }

                var requestId = payload.Value<string>("requestId");
                if (string.IsNullOrWhiteSpace(requestId))
                {
                    return;
                }

                var bodyPayload = await webView.CoreWebView2.CallDevToolsProtocolMethodAsync(
                    "Network.getResponseBody",
                    JsonConvert.SerializeObject(new { requestId }));
                var body = JObject.Parse(bodyPayload).Value<string>("body") ?? string.Empty;
                apiResponse.TrySetResult(new UsageApiResponse(
                    body,
                    response?.Value<int?>("status") ?? 200,
                    "页面原始请求"));
            }
            catch (Exception ex) when (ex is JsonException or InvalidOperationException or COMException)
            {
                apiResponse.TrySetException(ex);
            }
        }

        var receiver = webView.CoreWebView2.GetDevToolsProtocolEventReceiver("Network.responseReceived");
        webView.CoreWebView2.NavigationCompleted += OnNavigationCompleted;
        receiver.DevToolsProtocolEventReceived += OnResponseReceived;
        try
        {
            await webView.CoreWebView2.CallDevToolsProtocolMethodAsync("Network.enable", "{}");
            webView.CoreWebView2.Navigate(url);
            using (cancellationToken.Register(() => loaded.TrySetCanceled(cancellationToken)))
            {
                await loaded.Task;
            }

            var finished = await Task.WhenAny(apiResponse.Task, Task.Delay(2500, cancellationToken));
            return finished == apiResponse.Task ? await apiResponse.Task : null;
        }
        finally
        {
            receiver.DevToolsProtocolEventReceived -= OnResponseReceived;
            webView.CoreWebView2.NavigationCompleted -= OnNavigationCompleted;
        }
    }

    private static async Task<UsageApiResponse> FetchWhamUsageJson(WebView2 webView)
    {
        var script = $$"""
            (() => {
              const parseJson = text => {
                try { return JSON.parse(text || '{}'); } catch (_) { return {}; }
              };
              const findStringKey = (value, names, depth = 0) => {
                if (!value || typeof value !== 'object' || depth > 5) return '';
                for (const name of names) {
                  if (typeof value[name] === 'string' && value[name]) return value[name];
                }
                for (const item of Object.values(value)) {
                  const found = findStringKey(item, names, depth + 1);
                  if (found) return found;
                }
                return '';
              };
              const sendJsonRequest = (method, url) => {
                const xhr = new XMLHttpRequest();
                xhr.open(method, url, false);
                xhr.setRequestHeader('Accept', 'application/json');
                xhr.setRequestHeader('OAI-Language', navigator.language || 'zh-CN');
                try {
                  const timeZone = Intl.DateTimeFormat().resolvedOptions().timeZone;
                  if (timeZone) xhr.setRequestHeader('OAI-Time-Zone', timeZone);
                } catch (_) {}
                xhr.send();
                return xhr;
              };
              try {
                const sessionXhr = sendJsonRequest('GET', '/api/auth/session');
                const session = parseJson(sessionXhr.responseText);
                const accessToken = findStringKey(session, ['accessToken', 'access_token']);
                const accountId = findStringKey(session, ['account_id', 'accountId', 'accountID']);

                const xhr = new XMLHttpRequest();
                xhr.open('GET', {{JsonConvert.SerializeObject(UsageApiPath)}}, false);
                xhr.setRequestHeader('Accept', 'application/json');
                xhr.setRequestHeader('OAI-Language', navigator.language || 'zh-CN');
                try {
                  const timeZone = Intl.DateTimeFormat().resolvedOptions().timeZone;
                  if (timeZone) xhr.setRequestHeader('OAI-Time-Zone', timeZone);
                } catch (_) {}
                if (accessToken) xhr.setRequestHeader('Authorization', 'Bearer ' + accessToken);
                if (accountId) xhr.setRequestHeader('chatgpt-account-id', accountId);
                xhr.send();
                return JSON.stringify({
                  ok: xhr.status >= 200 && xhr.status < 300,
                  status: xhr.status,
                  statusText: xhr.statusText + '; session HTTP ' + sessionXhr.status + '; token=' + (accessToken ? 'yes' : 'no') + '; account=' + (accountId ? 'yes' : 'no'),
                  url: location.href,
                  body: xhr.responseText || ''
                });
              } catch (error) {
                return JSON.stringify({
                  ok: false,
                  status: 0,
                  statusText: String(error && (error.stack || error.message || error)),
                  url: location.href,
                  body: ''
                });
              }
            })();
            """;
        var raw = await webView.CoreWebView2.ExecuteScriptAsync(script);
        var envelopeText = DecodeScriptJson(raw);
        var envelope = JObject.Parse(envelopeText);
        if (envelope.Value<bool>("ok"))
        {
            return new UsageApiResponse(
                envelope.Value<string>("body") ?? string.Empty,
                envelope.Value<int?>("status") ?? 200,
                "fallback fetch");
        }

        var status = envelope.Value<int?>("status") ?? 0;
        var statusText = envelope.Value<string>("statusText") ?? string.Empty;
        var body = envelope.Value<string>("body") ?? string.Empty;
        throw new InvalidOperationException(BuildUsageApiHttpError(status, statusText, body));
    }

    private static string DecodeScriptJson(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        var trimmed = raw.TrimStart();
        return trimmed.StartsWith("\"", StringComparison.Ordinal)
            ? JsonConvert.DeserializeObject<string>(raw) ?? string.Empty
            : raw;
    }

    private static bool IsUsageApiUrl(string url) =>
        url.Contains(UsageApiPath, StringComparison.OrdinalIgnoreCase);

    private static string BuildUsageApiHttpError(int status, string statusText, string body)
    {
        var statusLabel = status > 0 ? status.ToString() : "unknown";
        return $"wham usage 接口返回 HTTP {statusLabel} {statusText}：{BuildBodyPreview(body)}".Trim();
    }

    private static string BuildUsageApiParseError(string body) =>
        $"未从 wham usage 接口解析到用量数据：{BuildBodyPreview(body)}";

    private static string BuildBodyPreview(string body)
    {
        var bodyPreview = (body ?? string.Empty).Trim();
        if (bodyPreview.Length > 180)
        {
            bodyPreview = bodyPreview[..180];
        }

        return string.IsNullOrWhiteSpace(bodyPreview) ? "响应为空" : bodyPreview;
    }

    private static async Task<string> NavigateAndReadVisibleText(WebView2 webView, string url, CancellationToken cancellationToken)
    {
        var loaded = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs args) => loaded.TrySetResult();

        webView.CoreWebView2.NavigationCompleted += OnNavigationCompleted;
        try
        {
            webView.CoreWebView2.Navigate(url);
            using (cancellationToken.Register(() => loaded.TrySetCanceled(cancellationToken)))
            {
                await loaded.Task;
            }

            await Task.Delay(1500, cancellationToken);
            return await ReadVisibleText(webView);
        }
        finally
        {
            webView.CoreWebView2.NavigationCompleted -= OnNavigationCompleted;
        }
    }

    /// <summary>
    /// 从 /backend-api/wham/usage JSON 中解析用量快照。
    /// </summary>
    public static UsageSnapshot? TryParseUsageApiJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            var root = JObject.Parse(json);
            var primaryWindow = root.SelectToken("rate_limit.primary_window");
            var secondaryWindow = root.SelectToken("rate_limit.secondary_window");
            if (primaryWindow is null || secondaryWindow is null)
            {
                return null;
            }

            return new UsageSnapshot
            {
                FiveHourRemainingPercent = UsedPercentToRemaining(primaryWindow.Value<int?>("used_percent")),
                WeeklyRemainingPercent = UsedPercentToRemaining(secondaryWindow.Value<int?>("used_percent")),
                FiveHourResetText = FormatUnixResetTime(primaryWindow.Value<long?>("reset_at")),
                WeeklyResetText = FormatUnixResetTime(secondaryWindow.Value<long?>("reset_at")),
                ExtraQuotaText = root.SelectToken("credits.balance")?.Value<string>() ?? "未知",
                RefreshedAt = DateTimeOffset.Now
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static async Task<string> TryReadChatGptCodexUsageText(
        WebView2 webView,
        string? accountDisplayName,
        CancellationToken cancellationToken)
    {
        var text = await NavigateAndReadVisibleText(webView, CodexHomeUrl, cancellationToken);
        if (TryParseVisibleText(text) is not null)
        {
            return text;
        }

        await TryOpenUsageDialog(webView, accountDisplayName);
        await Task.Delay(1200, cancellationToken);
        await TryOpenUsageDialog(webView, accountDisplayName);
        await Task.Delay(1200, cancellationToken);
        return await ReadVisibleText(webView);
    }

    private static async Task TryOpenUsageDialog(WebView2 webView, string? accountDisplayName)
    {
        var accountText = JsonConvert.SerializeObject(accountDisplayName ?? string.Empty);
        var script = $$"""
            (() => {
              const accountText = {{accountText}}.toLowerCase();
              const usagePattern = /(使用情况|usage)/i;
              const accountPattern = accountText ? new RegExp(accountText.replace(/[.*+?^${}()|[\]\\]/g, '\\$&'), 'i') : null;
              const isVisible = element => {
                const style = getComputedStyle(element);
                const rect = element.getBoundingClientRect();
                return style.visibility !== 'hidden' && style.display !== 'none' && rect.width > 0 && rect.height > 0;
              };
              const textOf = element => (element.innerText || element.textContent || '').trim();
              const clickFirst = predicate => {
                const elements = [...document.querySelectorAll('button,[role="button"],a,[tabindex],div')].filter(isVisible);
                const target = elements.find(predicate);
                if (!target) return false;
                target.click();
                return true;
              };

              if (clickFirst(element => usagePattern.test(textOf(element)))) return 'clicked-usage';
              if (accountPattern && clickFirst(element => accountPattern.test(textOf(element)))) return 'clicked-account';
              if (clickFirst(element => /(设置|settings|account|profile|套餐|plan)/i.test(textOf(element)))) return 'clicked-menu';
              return 'not-found';
            })();
            """;

        try
        {
            await webView.CoreWebView2.ExecuteScriptAsync(script);
        }
        catch (COMException)
        {
        }
        catch (InvalidOperationException)
        {
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

        var fiveHourPercent = ClampPercent(GetMatchValue(percentMatches[0]));
        var weeklyPercent = ClampPercent(GetMatchValue(percentMatches[1]));
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

    private static int UsedPercentToRemaining(int? usedPercent) =>
        Math.Clamp(100 - (usedPercent ?? 0), 0, 100);

    private static string FormatUnixResetTime(long? resetAt)
    {
        if (!resetAt.HasValue || resetAt.Value <= 0)
        {
            return "未知";
        }

        var localTime = DateTimeOffset.FromUnixTimeSeconds(resetAt.Value).LocalDateTime;
        return $"{localTime:yyyy年M月d日 H:mm}";
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
        return FirstNonEmpty(match.Groups.Cast<Group>().Skip(1).Select(group => group.Value).Append("未知").ToArray()).Trim();
    }

    private static string ExtractExtraQuota(string text)
    {
        var match = Regex.Match(text, @"剩余额度\s*[\r\n ]+(\d+)|可用\s*(\d+)\s*次|extra\s+quota\s*[\r\n :]+(\d+)", RegexOptions.IgnoreCase);
        return match.Success ? FirstNonEmpty(match.Groups.Cast<Group>().Skip(1).Select(group => group.Value).Append("未知").ToArray()) : "未知";
    }

    private static string GetMatchValue(Match match) =>
        FirstNonEmpty(match.Groups.Cast<Group>().Skip(1).Select(group => group.Value).ToArray());

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

    private sealed record UsageApiResponse(string Body, int StatusCode, string Source);
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
    public static UsageRefreshResult Success(UsageSnapshot snapshot, string message = "用量刷新完成。") => new(true, message, snapshot);

    /// <summary>
    /// 创建失败结果。
    /// </summary>
    public static UsageRefreshResult Failure(string message) => new(false, message, null);
}
