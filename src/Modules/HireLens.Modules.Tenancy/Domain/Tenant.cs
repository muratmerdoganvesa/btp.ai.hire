using HireLens.SharedKernel;

namespace HireLens.Modules.Tenancy.Domain;

public sealed class Tenant : ITenantEntity
{
    private Tenant()
    {
        Name = string.Empty;
        Slug = string.Empty;
    }

    public Guid Id { get; private set; }

    /// <summary>
    /// Catalog rows are themselves tenant-owned: TenantId equals Id so the
    /// global filter hides every other customer without a special case.
    /// </summary>
    public Guid TenantId { get; private set; }

    public string Name { get; private set; }

    public string Slug { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static Tenant Create(Guid id, string name, string slug, DateTimeOffset createdAt)
    {
        Guard.NotEmpty(id, nameof(id));
        Guard.NotNullOrWhiteSpace(name, nameof(name));
        Guard.NotNullOrWhiteSpace(slug, nameof(slug));

        return new Tenant
        {
            Id = id,
            TenantId = id,
            Name = name.Trim(),
            Slug = slug.Trim().ToLowerInvariant(),
            CreatedAt = createdAt
        };
    }

    public Result Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure(Error.Validation("Tenant name is required."));
        }

        Name = name.Trim();
        return Result.Success();
    }
}
