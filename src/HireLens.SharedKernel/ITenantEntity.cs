namespace HireLens.SharedKernel;

/// <summary>
/// Marker for every tenant-owned persistence model. HireLensDbContext binds a
/// global query filter to this interface via reflection so a new entity cannot
/// silently skip tenant isolation.
/// </summary>
public interface ITenantEntity
{
    Guid TenantId { get; }
}
