using HireLens.SharedKernel;

namespace HireLens.Modules.Taxonomy.Domain;

public sealed class SkillTerm : ITenantEntity
{
    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public string CanonicalName { get; private set; } = string.Empty;

    public string RawName { get; private set; } = string.Empty;

    public static SkillTerm Map(Guid tenantId, string raw, string canonical) =>
        new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            RawName = raw.Trim().ToLowerInvariant(),
            CanonicalName = canonical.Trim()
        };
}
