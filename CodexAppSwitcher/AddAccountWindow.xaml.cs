using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;
using Newtonsoft.Json;

namespace CodexAppSwitcher;

/// <summary>
/// 添加账号窗口。使用独立 WebView2 profile 登录并识别页面可见账号 ID。
/// </summary>
public partial class AddAccountWindow : Window
{
    private static readonly Regex EmailRegex = new(
        @"[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly string[] PlanTextMarkers = ["Free", "Plus", "Pro", "Team", "Enterprise"];

    private readonly string _profilePath;
    private readonly DispatcherTimer _autoDetectionTimer;
    private string _detectedUserIdentifier = string.Empty;
    private bool _hasDetectedUserIdentifier;
    private bool _isDetectingUserIdentifier;

    /// <summary>
    /// 创建添加账号窗口。
    /// </summary>
    public AddAccountWindow(string profilePath)
    {
        _profilePath = profilePath;
        InitializeComponent();
        _autoDetectionTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2)
        };
        _autoDetectionTimer.Tick += AutoDetectionTimer_OnTick;
    }

    /// <summary>
    /// 识别到的账号用户标识。
    /// </summary>
    public string UserIdentifier { get; private set; } = string.Empty;

    private async void Window_OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            Directory.CreateDirectory(_profilePath);
            var environment = await CoreWebView2Environment.CreateAsync(null, _profilePath);
            await Browser.EnsureCoreWebView2Async(environment);
            Browser.CoreWebView2.NavigationCompleted += Browser_OnNavigationCompleted;
            Browser.CoreWebView2.NewWindowRequested += Browser_OnNewWindowRequested;
            Browser.Source = new Uri("https://chatgpt.com/codex/cloud/settings/analytics");
            StatusText.Text = "页面已打开。登录成功后会自动尝试打开账号菜单并识别邮箱。";
            StartAutoDetection();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            StatusText.Text = $"WebView2 初始化失败：{ex.Message}";
        }
    }

    private void UseCurrentButton_OnClick(object sender, RoutedEventArgs e)
    {
        var identifier = NormalizeUserIdentifier(ManualUserIdentifierTextBox.Text);
        if (string.IsNullOrWhiteSpace(identifier))
        {
            identifier = _detectedUserIdentifier;
        }

        if (string.IsNullOrWhiteSpace(identifier))
        {
            StatusText.Text = "请先点击重新识别获取账号 ID，或在左侧手动填写账号 ID。";
            DetectedText.Text = "账号 ID：未识别";
            ShowThemedNotice(
                "缺少账号 ID",
                "请先点击“重新识别”获取账号 ID，或手动填写账号 ID。",
                "知道了");
            return;
        }

        var confirmation = ShowThemedConfirmation(
            "确认账号 ID",
            $"确认使用以下账号 ID？\n\n{identifier}",
            "使用当前账号",
            "返回修改");
        if (!confirmation)
        {
            StatusText.Text = "已取消使用当前账号，可重新识别或修改账号 ID。";
            return;
        }

        UserIdentifier = identifier;
        DetectedText.Text = $"账号 ID：{identifier}";
        DialogResult = true;
    }

    private async void DetectButton_OnClick(object sender, RoutedEventArgs e)
    {
        await TryOpenAccountMenu();
        var result = await TryReadUserIdentifier();
        if (string.IsNullOrWhiteSpace(result.Identifier))
        {
            StatusText.Text = $"未识别到账号 ID：{result.Message}";
            DetectedText.Text = "账号 ID：未识别";
            _detectedUserIdentifier = string.Empty;
            return;
        }

        _detectedUserIdentifier = result.Identifier;
        _hasDetectedUserIdentifier = true;
        _autoDetectionTimer.Stop();
        ManualUserIdentifierTextBox.Text = result.Identifier;
        DetectedText.Text = $"账号 ID：{result.Identifier}";
        StatusText.Text = $"已识别账号 ID（{result.Source}），请确认后点击使用当前账号。";
    }

    private void CancelButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (!ConfirmCancelWhenAccountIsReady())
        {
            return;
        }

        DialogResult = false;
    }

    private void TitleBar_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            ToggleWindowState();
            return;
        }

        DragMove();
    }

    private void MinimizeButton_OnClick(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void MaximizeButton_OnClick(object sender, RoutedEventArgs e)
    {
        ToggleWindowState();
    }

    private void CloseButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (!ConfirmCancelWhenAccountIsReady())
        {
            return;
        }

        DialogResult = false;
    }

    private void Browser_OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        StartAutoDetection();
    }

    private void Browser_OnNewWindowRequested(object? sender, CoreWebView2NewWindowRequestedEventArgs e)
    {
        if (IsGoogleSupportUri(e.Uri))
        {
            e.Handled = true;
            StatusText.Text = "已拦截 Google 帮助弹窗，请继续完成 Google 登录。";
            return;
        }

        e.Handled = true;
        Browser.CoreWebView2.Navigate(e.Uri);
    }

    private void AutoDetectionTimer_OnTick(object? sender, EventArgs e)
    {
        _ = TryAutoDetectUserIdentifier();
    }

    private void StartAutoDetection()
    {
        if (_hasDetectedUserIdentifier)
        {
            return;
        }

        _autoDetectionTimer.Stop();
        _autoDetectionTimer.Start();
        _ = TryAutoDetectUserIdentifier();
    }

    private async Task TryAutoDetectUserIdentifier()
    {
        if (_hasDetectedUserIdentifier || _isDetectingUserIdentifier)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(ManualUserIdentifierTextBox.Text))
        {
            return;
        }

        _isDetectingUserIdentifier = true;
        try
        {
            await TryOpenAccountMenu();
            var result = await TryReadUserIdentifier();
            if (string.IsNullOrWhiteSpace(result.Identifier))
            {
                StatusText.Text = "等待登录完成，或等待账号菜单可用后自动识别邮箱。";
                return;
            }

            _detectedUserIdentifier = result.Identifier;
            _hasDetectedUserIdentifier = true;
            _autoDetectionTimer.Stop();
            ManualUserIdentifierTextBox.Text = result.Identifier;
            DetectedText.Text = $"账号 ID：{result.Identifier}";
            StatusText.Text = $"已自动识别账号 ID（{result.Source}），请确认后点击使用当前账号。";
        }
        finally
        {
            _isDetectingUserIdentifier = false;
        }
    }

    private bool ConfirmCancelWhenAccountIsReady()
    {
        var identifier = NormalizeUserIdentifier(ManualUserIdentifierTextBox.Text);
        if (string.IsNullOrWhiteSpace(identifier))
        {
            identifier = _detectedUserIdentifier;
        }

        if (string.IsNullOrWhiteSpace(identifier))
        {
            return true;
        }

        return ShowThemedConfirmation(
            "确认取消添加",
            $"当前已获取账号 ID：\n\n{identifier}\n\n确认取消添加这个已登录的 ChatGPT 账号？",
            "确认取消",
            "继续添加");
    }

    private void ToggleWindowState()
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    private async Task<UserIdentifierDetectionResult> TryReadUserIdentifier()
    {
        var profileApiResult = await TryReadProfileApiUserIdentifier();
        if (!string.IsNullOrWhiteSpace(profileApiResult.Identifier))
        {
            return profileApiResult;
        }

        var visibleResult = await TryReadVisibleUserIdentifier();
        return !string.IsNullOrWhiteSpace(visibleResult.Identifier)
            ? visibleResult
            : new UserIdentifierDetectionResult(
                string.Empty,
                "未识别",
                "Profile API 未返回账号字段，页面文本也未找到邮箱。");
    }

    private async Task TryOpenAccountMenu()
    {
        if (Browser.CoreWebView2 is null)
        {
            return;
        }

        const string script = """
            (() => {
              if (!/^https:\/\/([^\/]+\.)?chatgpt\.com(\/|$)/i.test(location.href)) {
                return 'skip-non-chatgpt';
              }
              const emailPattern = /[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}/i;
              if (emailPattern.test(document.body ? document.body.innerText : '')) {
                return 'email-visible';
              }
              const isVisible = element => {
                const rect = element.getBoundingClientRect();
                const style = window.getComputedStyle(element);
                return rect.width > 20
                  && rect.height > 20
                  && rect.bottom > 0
                  && rect.right > 0
                  && rect.top < window.innerHeight
                  && rect.left < window.innerWidth
                  && style.visibility !== 'hidden'
                  && style.display !== 'none'
                  && Number(style.opacity || 1) > 0;
              };
              const candidates = Array.from(document.querySelectorAll('button,[role="button"],a'))
                .filter(isVisible)
                .map(element => {
                  const rect = element.getBoundingClientRect();
                  const text = `${element.innerText || ''} ${element.getAttribute('aria-label') || ''} ${element.getAttribute('title') || ''}`;
                  let score = 0;
                  if (rect.left < 320 && rect.bottom > window.innerHeight - 130) score += 80;
                  if (/account|profile|user|menu|avatar|账户|账号|个人/i.test(text)) score += 40;
                  if (/plus|pro|free|team|enterprise/i.test(text)) score += 20;
                  if (rect.left < 320) score += 10;
                  return { element, score };
                })
                .filter(item => item.score > 0)
                .sort((a, b) => b.score - a.score);
              if (!candidates.length) {
                return 'no-candidate';
              }
              candidates[0].element.click();
              return 'clicked';
            })();
            """;

        await Browser.CoreWebView2.ExecuteScriptAsync(script);
        await Task.Delay(500);
    }

    private async Task<UserIdentifierDetectionResult> TryReadProfileApiUserIdentifier()
    {
        if (Browser.CoreWebView2 is null)
        {
            return new UserIdentifierDetectionResult(string.Empty, "Profile API", "WebView2 尚未初始化。");
        }

        const string script = """
            (async () => {
              if (!/^https:\/\/([^\/]+\.)?chatgpt\.com(\/|$)/i.test(location.href)) {
                return {
                  ok: false,
                  source: '',
                  email: '',
                  userId: '',
                  name: '',
                  plan: '',
                  error: 'not chatgpt host'
                };
              }
              const forbidden = /(token|cookie|secret|session|csrf|jwt|access|refresh|auth)/i;
              const endpoints = [
                '/backend-api/me',
                '/backend-api/accounts/check/v4-2023-04-27'
              ];
              const result = {
                ok: false,
                source: '',
                email: '',
                userId: '',
                name: '',
                plan: '',
                error: ''
              };
              const setCandidate = (key, value) => {
                if (value === null || value === undefined || forbidden.test(key)) return;
                const text = String(value).trim();
                if (!text || text.length > 160) return;
                if (!result.email && /email/i.test(key) && /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(text)) result.email = text;
                if (!result.userId && /(^id$|user.?id|account.?id|sub$)/i.test(key) && !/[{}\[\]\s]/.test(text)) result.userId = text;
                if (!result.name && /(name|display.?name|username|handle)/i.test(key) && !/ChatGPT/i.test(text)) result.name = text;
                if (!result.plan && /(plan|product|subscription)/i.test(key)) result.plan = text;
              };
              const visit = (value, key = '') => {
                if (value === null || value === undefined || forbidden.test(key)) return;
                if (typeof value !== 'object') {
                  setCandidate(key, value);
                  return;
                }
                if (Array.isArray(value)) {
                  value.forEach(item => visit(item, key));
                  return;
                }
                Object.entries(value).forEach(([childKey, childValue]) => visit(childValue, childKey));
              };
              for (const endpoint of endpoints) {
                try {
                  const response = await fetch(endpoint, {
                    credentials: 'include',
                    headers: { accept: 'application/json' }
                  });
                  if (!response.ok) {
                    result.error = `${endpoint}: HTTP ${response.status}`;
                    continue;
                  }
                  const contentType = response.headers.get('content-type') || '';
                  if (!contentType.includes('application/json')) {
                    result.error = `${endpoint}: non-json`;
                    continue;
                  }
                  const body = await response.json();
                  visit(body);
                  if (result.email || result.userId || result.name) {
                    result.ok = true;
                    result.source = endpoint;
                    return result;
                  }
                  result.error = `${endpoint}: no whitelisted fields`;
                } catch (error) {
                  result.error = `${endpoint}: ${error && error.message ? error.message : 'request failed'}`;
                }
              }
              return result;
            })();
            """;

        var rawResult = await Browser.CoreWebView2.ExecuteScriptAsync(script);
        var result = JsonConvert.DeserializeObject<ProfileApiDetectionResult>(rawResult);
        if (result is null || !result.Ok)
        {
            return new UserIdentifierDetectionResult(
                string.Empty,
                "Profile API",
                result?.Error ?? "Profile API 未返回可用账号字段。");
        }

        var identifier = FirstNonEmpty(result.Email, result.UserId);
        return new UserIdentifierDetectionResult(identifier, $"Profile API {result.Source}", string.Empty);
    }

    private async Task<UserIdentifierDetectionResult> TryReadVisibleUserIdentifier()
    {
        if (Browser.CoreWebView2 is null)
        {
            return new UserIdentifierDetectionResult(string.Empty, "页面文本", "WebView2 尚未初始化。");
        }

        const string script = """
            (() => {
              if (!/^https:\/\/([^\/]+\.)?chatgpt\.com(\/|$)/i.test(location.href)) {
                return '';
              }
              const values = [];
              const push = value => {
                if (value && String(value).trim()) values.push(String(value).trim());
              };
              push(document.body ? document.body.innerText : '');
              document
                .querySelectorAll('[aria-label],[title],[alt],[placeholder]')
                .forEach(element => {
                  push(element.getAttribute('aria-label'));
                  push(element.getAttribute('title'));
                  push(element.getAttribute('alt'));
                  push(element.getAttribute('placeholder'));
                });
              document
                .querySelectorAll('button,a,[role="button"],[role="menuitem"]')
                .forEach(element => push(element.innerText || element.textContent));
              return values.join('\n');
            })();
            """;
        var rawResult = await Browser.CoreWebView2.ExecuteScriptAsync(script);
        var visibleText = JsonConvert.DeserializeObject<string>(rawResult) ?? string.Empty;
        return new UserIdentifierDetectionResult(ExtractEmail(visibleText), "页面文本邮箱", string.Empty);
    }

    private static string NormalizeUserIdentifier(string value)
    {
        var text = value.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var emailMatch = EmailRegex.Match(text);
        if (emailMatch.Success && emailMatch.Value.Length == text.Length)
        {
            return emailMatch.Value;
        }

        return IsLikelyDisplayName(text) ? text : string.Empty;
    }

    private static string ExtractEmail(string text)
    {
        var emailMatch = EmailRegex.Match(text);
        return emailMatch.Success ? emailMatch.Value : string.Empty;
    }

    private static bool IsGoogleSupportUri(string value)
    {
        return Uri.TryCreate(value, UriKind.Absolute, out var uri)
            && uri.Host.Equals("support.google.com", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPlanMarker(string value)
    {
        foreach (var marker in PlanTextMarkers)
        {
            if (string.Equals(value, marker, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsLikelyDisplayName(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length is < 2 or > 80)
        {
            return false;
        }

        if (value.Contains("ChatGPT", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return !IsPlanMarker(value);
    }

    private static string FirstNonEmpty(params string?[] values)
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

    private bool ShowThemedConfirmation(string title, string message, string primaryText, string secondaryText)
    {
        return ShowThemedDialog(title, message, primaryText, secondaryText) == true;
    }

    private void ShowThemedNotice(string title, string message, string primaryText)
    {
        ShowThemedDialog(title, message, primaryText, null);
    }

    private bool? ShowThemedDialog(string title, string message, string primaryText, string? secondaryText)
    {
        var dialog = new Window
        {
            Owner = this,
            Title = title,
            Width = 420,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            WindowStyle = WindowStyle.None,
            ResizeMode = ResizeMode.NoResize,
            Background = (Brush)FindResource("PanelBrush"),
            Foreground = (Brush)FindResource("TextPrimaryBrush"),
            FontFamily = FontFamily,
            ShowInTaskbar = false
        };

        var root = new Border
        {
            Background = (Brush)FindResource("PanelBrush"),
            BorderBrush = (Brush)FindResource("BorderBrush"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(18)
        };

        var layout = new Grid();
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var titleBlock = new TextBlock
        {
            Text = title,
            FontSize = 16,
            Foreground = (Brush)FindResource("TextPrimaryBrush"),
            Margin = new Thickness(0, 0, 0, 12)
        };
        Grid.SetRow(titleBlock, 0);
        layout.Children.Add(titleBlock);

        var content = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 0, 0, 18)
        };
        var badge = new Border
        {
            Width = 28,
            Height = 28,
            CornerRadius = new CornerRadius(14),
            Background = (Brush)FindResource("ControlBrush"),
            BorderBrush = (Brush)FindResource("AccentBrush"),
            BorderThickness = new Thickness(1),
            Margin = new Thickness(0, 2, 12, 0)
        };
        badge.Child = new TextBlock
        {
            Text = "!",
            Foreground = (Brush)FindResource("AccentBrush"),
            FontSize = 16,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        content.Children.Add(badge);
        content.Children.Add(new TextBlock
        {
            Text = message,
            Foreground = (Brush)FindResource("TextPrimaryBrush"),
            FontSize = 13,
            TextWrapping = TextWrapping.Wrap,
            Width = 320,
            LineHeight = 22
        });
        Grid.SetRow(content, 1);
        layout.Children.Add(content);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        if (!string.IsNullOrWhiteSpace(secondaryText))
        {
            var secondaryButton = new Button
            {
                Content = secondaryText,
                Width = 96,
                Height = 34,
                Margin = new Thickness(0, 0, 8, 0)
            };
            secondaryButton.Click += (_, _) =>
            {
                dialog.DialogResult = false;
                dialog.Close();
            };
            buttons.Children.Add(secondaryButton);
        }

        var primaryButton = new Button
        {
            Content = primaryText,
            Width = 116,
            Height = 34,
            Style = (Style)FindResource("PrimaryActionButtonStyle")
        };
        primaryButton.Click += (_, _) =>
        {
            dialog.DialogResult = true;
            dialog.Close();
        };
        buttons.Children.Add(primaryButton);
        Grid.SetRow(buttons, 2);
        layout.Children.Add(buttons);

        root.Child = layout;
        dialog.Content = root;
        return dialog.ShowDialog();
    }

    private sealed class ProfileApiDetectionResult
    {
        [JsonProperty("ok")]
        public bool Ok { get; set; }

        [JsonProperty("source")]
        public string Source { get; set; } = string.Empty;

        [JsonProperty("email")]
        public string Email { get; set; } = string.Empty;

        [JsonProperty("userId")]
        public string UserId { get; set; } = string.Empty;

        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;

        [JsonProperty("error")]
        public string Error { get; set; } = string.Empty;
    }

    private sealed record UserIdentifierDetectionResult(string Identifier, string Source, string Message);
}
