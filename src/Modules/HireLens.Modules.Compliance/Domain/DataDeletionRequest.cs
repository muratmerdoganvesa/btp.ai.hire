using HireLens.SharedKernel;

namespace HireLens.Modules.Compliance.Domain;

public sealed class DataDeletionRequest : ITenantEntity
{
    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid CandidateId { get; private set; }

    public string Reason { get; private set; } = string.Empty;

    public string Status { get; private set; } = "received";

    public DateTimeOffset RequestedAt { get; private set; }

    public static DataDeletionRequest Open(Guid tenantId, Guid candidateId, string reason, DateTimeOffset now) =>
        new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CandidateId = candidateId,
            Reason = string.IsNullOrWhiteSpace(reason) ? "data_subject_request" : reason.Trim(),
            Status = "received",
            RequestedAt = now
        };

    public void Complete() => Status = "anonymized";
}
