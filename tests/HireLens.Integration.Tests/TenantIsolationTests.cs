using System.Net;
using FluentAssertions;
using Xunit;
using HireLens.Contracts;

namespace HireLens.Integration.Tests;

public sealed class TenantIsolationTests : IClassFixture<HireLensApiFactory>
{
    private readonly HireLensApiFactory _factory;

    public TenantIsolationTests(HireLensApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Cross_tenant_user_read_returns_404_not_403()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        using var anonymous = _factory.CreateClient();
        var tokenA = await TestAuth.IssueTokenAsync(anonymous, tenantA, "user-a", Roles.TenantAdmin);
        var tokenB = await TestAuth.IssueTokenAsync(anonymous, tenantB, "user-b", Roles.TenantAdmin);

        using var clientA = _factory.As(tokenA);
        using var clientB = _factory.As(tokenB);

        var userB = await TestAuth.CreateUserAsync(clientB, "subject-b", "User B", [Roles.Recruiter]);

        using var response = await clientA.GetAsync($"/api/identity/users/{userB.Id}");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Cross_tenant_tenant_read_returns_404()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        using var anonymous = _factory.CreateClient();
        var tokenA = await TestAuth.IssueTokenAsync(anonymous, tenantA, "user-a", Roles.Recruiter);
        await TestAuth.IssueTokenAsync(anonymous, tenantB, "user-b", Roles.Recruiter);

        using var clientA = _factory.As(tokenA);
        using var response = await clientA.GetAsync($"/api/tenants/{tenantB}");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
