using HireLens.SharedKernel;

namespace HireLens.Modules.Privacy.Domain;

public sealed class ConsentRecord : ITenantEntity
{
    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid CandidateId { get; private set; }

    public string Purpose { get; private set; } = string.Empty;

    public DateTimeOffset AcceptedAt { get; private set; }

    public static ConsentRecord Grant(Guid tenantId, Guid candidateId, string purpose, DateTimeOffset now) =>
        new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CandidateId = candidateId,
            Purpose = purpose,
            AcceptedAt = now
        };
}
