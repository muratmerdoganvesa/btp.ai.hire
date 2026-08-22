namespace HireLens.Contracts.Tenancy;

public sealed record TenantDto(
    Guid Id,
    string Name,
    string Slug,
    DateTimeOffset CreatedAt);

public sealed record UpdateTenantRequest(string Name);

public sealed record TenantProvisioned(Guid TenantId, string Slug) : SharedKernel.DomainEvent;

public interface ITenantProvisionPort
{
    Task<SharedKernel.Result<TenantDto>> ProvisionAsync(Guid tenantId, string name, string slug, CancellationToken cancellationToken);
}
