using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using CodexAppSwitcher.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CodexAppSwitcher.Services;

/// <summary>
/// 账号身份识别服务。只提取邮箱等非敏感标识，不输出 token 或 cookie。
/// </summary>
public sealed class AccountIdentityService
{
    private static readonly Regex EmailRegex = new(
        @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private readonly CodexPathResolver _pathResolver;

    /// <summary>
    /// 创建账号身份识别服务。
    /// </summary>
    public AccountIdentityService(CodexPathResolver pathResolver)
    {
        _pathResolver = pathResolver;
    }

    /// <summary>
    /// 识别当前账号用户标识。
    /// </summary>
    public AccountIdentitySnapshot DetectCurrentIdentity()
    {
        var fromAuth = TryDetectFromJsonFile(_pathResolver.AuthJsonPath, "auth.json");
        if (fromAuth.HasUserIdentifier)
        {
            return fromAuth;
        }

        return new AccountIdentitySnapshot
        {
            UserIdentifier = string.Empty,
            Source = "等待 WebView2 登录识别"
        };
    }

    private static AccountIdentitySnapshot TryDetectFromJsonFile(string filePath, string source)
    {
        if (!File.Exists(filePath))
        {
            return new AccountIdentitySnapshot();
        }

        try
        {
            var text = File.ReadAllText(filePath);
            var token = JToken.Parse(text);
            var email = FindEmailByPropertyName(token) ?? TryGetEmailFromIdToken(token);
            return new AccountIdentitySnapshot
            {
                UserIdentifier = email ?? string.Empty,
                Source = email is null ? "未识别" : source
            };
        }
        catch (IOException)
        {
            return new AccountIdentitySnapshot();
        }
        catch (UnauthorizedAccessException)
        {
            return new AccountIdentitySnapshot();
        }
        catch (JsonException)
        {
            return new AccountIdentitySnapshot();
        }
    }

    private static string? FindEmailByPropertyName(JToken token)
    {
        if (token is JProperty property)
        {
            var propertyEmail = TryGetEmailFromProperty(property);
            if (propertyEmail is not null)
            {
                return propertyEmail;
            }
        }

        foreach (var child in token.Children())
        {
            var childEmail = FindEmailByPropertyName(child);
            if (childEmail is not null)
            {
                return childEmail;
            }
        }

        return null;
    }

    private static string? TryGetEmailFromProperty(JProperty property)
    {
        if (!property.Name.Contains("email", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var value = property.Value.Type == JTokenType.String ? property.Value.Value<string>() : null;
        return !string.IsNullOrWhiteSpace(value) && EmailRegex.IsMatch(value) ? value : null;
    }

    private static string? TryGetEmailFromIdToken(JToken token)
    {
        var idToken = token.SelectToken("tokens.id_token")?.Value<string>();
        if (string.IsNullOrWhiteSpace(idToken))
        {
            return null;
        }

        var parts = idToken.Split('.');
        if (parts.Length < 2)
        {
            return null;
        }

        try
        {
            var payload = JToken.Parse(Encoding.UTF8.GetString(Base64UrlDecode(parts[1])));
            var email = payload.SelectToken("email")?.Value<string>();
            return !string.IsNullOrWhiteSpace(email) && EmailRegex.IsMatch(email) ? email : null;
        }
        catch (Exception ex) when (ex is FormatException or JsonException)
        {
            return null;
        }
    }

    private static byte[] Base64UrlDecode(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        padded = padded.PadRight(padded.Length + ((4 - padded.Length % 4) % 4), '=');
        return Convert.FromBase64String(padded);
    }
}
