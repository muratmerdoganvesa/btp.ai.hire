using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace HireLens.Integration.Tests;

public sealed class AuthChallengeTests
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    [Fact]
    public async Task Me_without_token_returns_json_challenge()
    {
        using var factory = new HireLensApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/me");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var payload = await response.Content.ReadFromJsonAsync<Challenge>(Json);
        payload.Should().NotBeNull();
        payload!.Error.Should().Be("jwt_rejected");
        payload.HasAuthorization.Should().BeFalse();
    }

    private sealed record Challenge(string Error, string? Detail, bool HasAuthorization);
}
