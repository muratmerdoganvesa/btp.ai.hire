using HireLens.Contracts.Tenancy;
using HireLens.Infrastructure.Persistence;
using HireLens.Modules.Tenancy.Domain;
using HireLens.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace HireLens.Modules.Tenancy.Application;

public interface ITenantService
{
    Task<Result<TenantDto>> GetCurrentAsync(CancellationToken cancellationToken);

    Task<Result<TenantDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<Result<TenantDto>> UpdateCurrentAsync(UpdateTenantRequest request, CancellationToken cancellationToken);

    Task<Result<TenantDto>> ProvisionAsync(Guid tenantId, string name, string slug, CancellationToken cancellationToken);
}

public sealed class TenantService(
    HireLensDbContext db,
    ITenantContext tenantContext,
    IClock clock) : ITenantService, ITenantProvisionPort
{
    public async Task<Result<TenantDto>> GetCurrentAsync(CancellationToken cancellationToken)
    {
        RepositoryGuard.RequireTenant(tenantContext);
        var tenant = await db.Set<Tenant>().SingleOrDefaultAsync(cancellationToken);
        return tenant is null
            ? Result.Failure<TenantDto>(Error.NotFound("Tenant was not found."))
            : Result.Success(ToDto(tenant));
    }

    public async Task<Result<TenantDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        RepositoryGuard.RequireTenant(tenantContext);
        var tenant = await db.Set<Tenant>().SingleOrDefaultAsync(t => t.Id == id, cancellationToken);
        return tenant is null
            ? Result.Failure<TenantDto>(Error.NotFound("Tenant was not found."))
            : Result.Success(ToDto(tenant));
    }

    public async Task<Result<TenantDto>> UpdateCurrentAsync(UpdateTenantRequest request, CancellationToken cancellationToken)
    {
        RepositoryGuard.RequireTenant(tenantContext);
        var tenant = await db.Set<Tenant>().SingleOrDefaultAsync(cancellationToken);
        if (tenant is null)
        {
            return Result.Failure<TenantDto>(Error.NotFound("Tenant was not found."));
        }

        var renamed = tenant.Rename(request.Name);
        if (renamed.IsFailure)
        {
            return Result.Failure<TenantDto>(renamed.Error);
        }

        await db.SaveChangesAsync(cancellationToken);
        return Result.Success(ToDto(tenant));
    }

    public async Task<Result<TenantDto>> ProvisionAsync(
        Guid tenantId,
        string name,
        string slug,
        CancellationToken cancellationToken)
    {
        RepositoryGuard.RequireTenant(tenantContext);

        if (await db.Set<Tenant>().AnyAsync(t => t.Slug == slug, cancellationToken))
        {
            return Result.Failure<TenantDto>(Error.Conflict("A tenant with this slug already exists."));
        }

        var tenant = Tenant.Create(tenantId, name, slug, clock.UtcNow);
        db.Set<Tenant>().Add(tenant);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success(ToDto(tenant));
    }

    private static TenantDto ToDto(Tenant tenant) =>
        new(tenant.Id, tenant.Name, tenant.Slug, tenant.CreatedAt);
}
