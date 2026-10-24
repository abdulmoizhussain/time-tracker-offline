using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.RegularExpressions;
using TimeTracker.Core.Models;

namespace TimeTracker.App.Services;

public sealed record JiraWorklogResult(bool Success, string TicketId, string Details);

public sealed class JiraWorklogClient(HttpClient httpClient)
{
    private static readonly Regex TicketPattern = new(@"([A-Za-z]+-\d+)", RegexOptions.ECMAScript, TimeSpan.FromSeconds(1));

    public static DateTime EntryStartUtc(TimeEntry entry) => entry.StartUtc ?? entry.EndUtc ?? entry.CreatedUtc;

    public async Task<JiraWorklogResult> UploadAsync(TimeEntry entry, string domain, string email, string token,
        CancellationToken cancellationToken = default)
    {
        var match = TicketPattern.Match(entry.Description);
        if (!match.Success) return new(false, "", "No ticket ID found in description.");
        var ticket = match.Value;
        if (entry.IsRunning) return new(false, ticket, "Running entries cannot be uploaded.");
        if (entry.DurationSeconds < 60) return new(false, ticket, "Duration shorter than 1 minute.");
        var start = EntryStartUtc(entry);
        if (start == default) return new(false, ticket, "Missing start date or time.");

        domain = JiraCredentials.NormalizeDomain(domain);
        var authorization = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{email}:{token}"));
        var payload = new
        {
            comment = new
            {
                type = "doc", version = 1,
                content = new[] { new { type = "paragraph", content = new[] { new { type = "text", text = entry.Description } } } }
            },
            started = start.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fff'+0000'", CultureInfo.InvariantCulture),
            timeSpentSeconds = entry.DurationSeconds
        };
        using var request = new HttpRequestMessage(HttpMethod.Post, $"https://{domain}/rest/api/3/issue/{ticket}/worklog");
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", authorization);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Content = JsonContent.Create(payload);
        try
        {
            using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                // A confirmed successful POST stays successful even if Jira's response body is empty or malformed.
                return new(true, ticket, "Work log created.");
            }
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            body = Redact(body, token, authorization).Replace('\r', ' ').Replace('\n', ' ');
            if (body.Length > 1500) body = body[..1500];
            var hint = (int)response.StatusCode == 401 ? " Check your email/token in Connection settings." : "";
            return new(false, ticket, $"API error: {(int)response.StatusCode} {response.ReasonPhrase}.{hint} {body}".Trim());
        }
        catch (HttpRequestException)
        {
            return new(false, ticket, "Network error. Upload outcome may be unknown; check Jira before retrying.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new(false, ticket, "Request timed out. Upload outcome may be unknown; check Jira before retrying.");
        }
    }

    public static string Redact(string text, string token, string authorization)
    {
        if (!string.IsNullOrEmpty(authorization)) text = text.Replace(authorization, "[redacted]", StringComparison.Ordinal);
        if (!string.IsNullOrEmpty(token)) text = text.Replace(token, "[redacted]", StringComparison.Ordinal);
        return text;
    }
}
