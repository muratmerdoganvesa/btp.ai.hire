using System.Net;
using FluentAssertions;
using Xunit;

namespace HireLens.Integration.Tests;

public sealed class HealthSmokeTests : IClassFixture<HireLensApiFactory>
{
    private readonly HireLensApiFactory _factory;

    public HealthSmokeTests(HireLensApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Live_endpoint_returns_ok()
    {
        using var client = _factory.CreateClient();
        using var response = await client.GetAsync("/health/live");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Ready_endpoint_returns_ok_with_in_memory_database()
    {
        using var client = _factory.CreateClient();
        using var response = await client.GetAsync("/health/ready");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
