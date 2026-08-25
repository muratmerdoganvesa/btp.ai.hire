using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HireLens.AiGateway.Providers;

/// <summary>
/// Caches XSUAA client_credentials tokens and refreshes under a single lock
/// so concurrent CV jobs do not stampede the token endpoint.
/// </summary>
public sealed class AiCoreTokenProvider(
    HttpClient httpClient,
    IOptions<SapAiCoreOptions> options,
    ILogger<AiCoreTokenProvider> logger)
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private string? _accessToken;
    private DateTimeOffset _expiresAt = DateTimeOffset.MinValue;

    public async Task<string> GetTokenAsync(CancellationToken cancellationToken)
    {
        if (_accessToken is not null && DateTimeOffset.UtcNow < _expiresAt)
        {
            return _accessToken;
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_accessToken is not null && DateTimeOffset.UtcNow < _expiresAt)
            {
                return _accessToken;
            }

            var binding = ResolveBinding();
            using var request = new HttpRequestMessage(HttpMethod.Post, binding.TokenUrl);
            var raw = $"{binding.ClientId}:{binding.ClientSecret}";
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
            var token = document.RootElement.GetProperty("access_token").GetString()
                ?? throw new InvalidOperationException("AI Core token response omitted access_token.");
            var expiresIn = document.RootElement.TryGetProperty("expires_in", out var exp)
                ? exp.GetInt32()
                : 3600;

            _accessToken = token;
            _expiresAt = DateTimeOffset.UtcNow.AddSeconds(Math.Max(60, expiresIn - 60));
            logger.LogDebug("AI Core token refreshed; expires at {ExpiresAt:u}", _expiresAt);
            return token;
        }
        finally
        {
            _gate.Release();
        }
    }

    public AiCoreBinding ResolveBinding()
    {
        var opts = options.Value;
        if (!string.IsNullOrWhiteSpace(opts.ServiceKeyJson))
        {
            return SapOrchestrationProvider.ParseBinding(opts.ServiceKeyJson);
        }

        if (string.IsNullOrWhiteSpace(opts.ClientId)
            || string.IsNullOrWhiteSpace(opts.ClientSecret)
            || string.IsNullOrWhiteSpace(opts.XsuaaUrl)
            || string.IsNullOrWhiteSpace(opts.AiApiUrl))
        {
            throw new InvalidOperationException(
                "Configure AICORE_SERVICE_KEY or AiCore ClientId/ClientSecret/XsuaaUrl/AiApiUrl.");
        }

        var tokenUrl = opts.XsuaaUrl.TrimEnd('/');
        if (!tokenUrl.Contains("oauth/token", StringComparison.OrdinalIgnoreCase))
        {
            tokenUrl += "/oauth/token";
        }

        return new AiCoreBinding(tokenUrl, opts.ClientId, opts.ClientSecret, opts.AiApiUrl.TrimEnd('/'));
    }
}
