using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;

namespace TimeTracker.App.Services;

public static class JiraCredentials
{
    public static string NormalizeDomain(string input)
    {
        var text = input.Trim();
        if (!text.Contains("://", StringComparison.Ordinal)) text = "https://" + text;
        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps
            || string.IsNullOrEmpty(uri.Host) || uri.AbsolutePath != "/" || uri.UserInfo.Length != 0
            || uri.Query.Length != 0 || uri.Fragment.Length != 0)
            throw new ArgumentException("Enter your Jira domain, for example company.atlassian.net, with optional https:// and no path.");
        return uri.Authority.ToLowerInvariant();
    }

    public static string ValidateEmail(string input)
    {
        var email = input.Trim();
        if (!MailAddress.TryCreate(email, out var address) || address.Address != email || email.Contains(':'))
            throw new ArgumentException("Enter a valid Jira account email address.");
        return email;
    }

    public static string Protect(string token) => Convert.ToBase64String(ProtectedData.Protect(
        Encoding.UTF8.GetBytes(token), null, DataProtectionScope.CurrentUser));

    public static string Unprotect(string protectedToken) => Encoding.UTF8.GetString(ProtectedData.Unprotect(
        Convert.FromBase64String(protectedToken), null, DataProtectionScope.CurrentUser));
}
