using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using HireLens.Api.Auth;
using HireLens.Contracts.Identity;

namespace HireLens.Integration.Tests;

internal static class TestAuth
{

    public static async Task<string> IssueTokenAsync(
        HttpClient client,
        Guid tenantId,
        string subject,
        params string[] roles)
    {
        using var response = await client.PostAsJsonAsync("/dev/token", new DevTokenRequest(
            tenantId,
            subject,
            roles,
            "xsuaa"));
        response.EnsureSuccessStatusCode();
        var raw = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(raw);
        var token = document.RootElement.TryGetProperty("accessToken", out var access)
            ? access.GetString()
            : document.RootElement.TryGetProperty("AccessToken", out var accessPascal)
                ? accessPascal.GetString()
                : null;
        if (string.IsNullOrWhiteSpace(token) || token.Count(c => c == '.') != 2)
        {
            throw new InvalidOperationException($"Dev token was malformed: {raw}");
        }

        return token;
    }

    public static HttpClient As(this HireLensApiFactory factory, string token)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    public static async Task<UserDto> CreateUserAsync(HttpClient client, string subject, string displayName, string[] roles)
    {
        using var response = await client.PostAsJsonAsync("/api/identity/users", new CreateUserRequest(subject, displayName, roles));
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException(
                $"{(int)response.StatusCode} {response.ReasonPhrase} {body} jwt={HireLens.Api.Auth.JwtDebug.LastFailure}");
        }
        return await response.Content.ReadFromJsonAsync<UserDto>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("Create user returned an empty body.");
    }
}
