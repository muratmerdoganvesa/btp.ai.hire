using System.Security.Claims;
using FluentAssertions;
using Xunit;
using HireLens.Infrastructure.Tenancy;

namespace HireLens.Unit.Tests;

public sealed class TenantClaimTests
{
    [Fact]
    public void Xsuaa_issuer_reads_zid()
    {
        var tenantId = Guid.NewGuid();
        var user = Principal("https://hirelens.authentication.eu10.hana.ondemand.com/oauth/token", "zid", tenantId);

        TenantResolutionMiddleware.ReadTenantId(user).Should().Be(tenantId);
    }

    [Fact]
    public void Ias_issuer_reads_app_tid()
    {
        var tenantId = Guid.NewGuid();
        var user = Principal("https://accounts.ondemand.com", "app_tid", tenantId);

        TenantResolutionMiddleware.ReadTenantId(user).Should().Be(tenantId);
    }

    [Fact]
    public void Missing_tenant_claim_is_unresolved()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("iss", "https://hirelens.authentication.eu10.hana.ondemand.com/oauth/token")
        ], "test"));

        TenantResolutionMiddleware.ReadTenantId(user).Should().BeNull();
    }

    [Fact]
    public void Non_guid_zid_is_stable()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("iss", "https://hirelens.authentication.eu10.hana.ondemand.com/oauth/token"),
            new Claim("zid", "dev-2z4ga5d8"),
            new Claim("sub", "tester")
        ], "test"));

        var first = TenantResolutionMiddleware.ReadTenantId(user);
        var second = TenantResolutionMiddleware.ReadTenantId(user);
        first.Should().NotBeNull();
        first.Should().Be(second);
    }

    private static ClaimsPrincipal Principal(string issuer, string claim, Guid tenantId) =>
        new(new ClaimsIdentity(
        [
            new Claim("iss", issuer),
            new Claim(claim, tenantId.ToString()),
            new Claim("sub", "tester")
        ], "test"));
}
