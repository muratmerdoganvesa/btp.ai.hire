using FluentAssertions;
using Xunit;
using HireLens.Contracts;
using HireLens.Infrastructure.Persistence;
using HireLens.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HireLens.Integration.Tests;

public sealed class AuditTrailTests : IClassFixture<HireLensApiFactory>
{
    private readonly HireLensApiFactory _factory;

    public AuditTrailTests(HireLensApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Every_write_produces_an_audit_event()
    {
        var tenantId = Guid.NewGuid();
        using var anonymous = _factory.CreateClient();
        var token = await TestAuth.IssueTokenAsync(anonymous, tenantId, "auditor", Roles.TenantAdmin);
        using var client = _factory.As(token);

        await TestAuth.CreateUserAsync(client, "subject-audit", "Audit User", [Roles.Recruiter]);

        await using var scope = _factory.Services.CreateAsyncScope();
        var tenantContext = scope.ServiceProvider.GetRequiredService<TenantContext>();
        tenantContext.Resolve(tenantId, "auditor", "audit-test");
        var db = scope.ServiceProvider.GetRequiredService<HireLensDbContext>();

        var events = await db.AuditEvents.ToListAsync();
        events.Should().Contain(e => e.EntityType == "TenantUser" && e.Action == "Added");
        events.Should().OnlyContain(e => e.TenantId == tenantId);
    }
}
