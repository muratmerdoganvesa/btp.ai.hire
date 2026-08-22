using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using HireLens.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace HireLens.Infrastructure.Btp;

/// <summary>
/// Ships already-persisted AuditEvent rows to SAP Audit Log Service when a
/// binding exists. Path is isolated so an API version change stays here.
/// Documented write surface: POST {url}/audit-log/oauth2/v2/configuration-changes
/// </summary>
public sealed class SapAuditLogSink(
    HttpClient httpClient,
    VcapService binding,
    ILogger<SapAuditLogSink> logger) : IAuditSink
{
    public async Task WriteAsync(IReadOnlyList<AuditEvent> events, CancellationToken cancellationToken = default)
    {
        if (events.Count == 0)
        {
            return;
        }

        try
        {
            var token = await RequestTokenAsync(cancellationToken);
            foreach (var auditEvent in events)
            {
                using var request = new HttpRequestMessage(
                    HttpMethod.Post,
                    Combine(binding.Credentials.Url, "/audit-log/oauth2/v2/configuration-changes"));

                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                request.Content = new StringContent(Serialize(auditEvent), Encoding.UTF8, "application/json");

                using var response = await httpClient.SendAsync(request, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    logger.LogWarning(
                        "SAP Audit Log rejected {Action} on {Entity} with {Status}",
                        auditEvent.Action,
                        auditEvent.EntityType,
                        (int)response.StatusCode);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "SAP Audit Log sink failed; local AuditEvent rows remain");
        }
    }

    private async Task<string> RequestTokenAsync(CancellationToken cancellationToken)
    {
        var tokenUrl = binding.Credentials.Extra.TryGetValue("uaa.url", out var uaa)
            ? Combine(uaa, "/oauth/token")
            : Combine(binding.Credentials.Url, "/oauth/token");

        using var request = new HttpRequestMessage(HttpMethod.Post, tokenUrl);
        var raw = $"{binding.Credentials.ClientId}:{binding.Credentials.ClientSecret}";
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes(raw)));
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials"
        });

        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        return document.RootElement.GetProperty("access_token").GetString()
            ?? throw new InvalidOperationException("Audit Log token response omitted access_token.");
    }

    private static string Serialize(AuditEvent auditEvent) =>
        JsonSerializer.Serialize(new
        {
            user = auditEvent.ActorSubject ?? "system",
            tenant = auditEvent.TenantId.ToString(),
            target = new { type = auditEvent.EntityType, id = auditEvent.EntityId },
            attributes = new[]
            {
                new { name = "action", old = (string?)null, @new = auditEvent.Action }
            }
        });

    private static string Combine(string? root, string path)
    {
        var baseUrl = (root ?? string.Empty).TrimEnd('/');
        return baseUrl + path;
    }
}
