using HireLens.SharedKernel;

namespace HireLens.Modules.Review.Domain;

public sealed class Decision : ITenantEntity
{
    private Decision()
    {
        Outcome = string.Empty;
        Rationale = string.Empty;
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid CandidateId { get; private set; }

    public string Outcome { get; private set; }

    public string Rationale { get; private set; }

    public DateTimeOffset DecidedAt { get; private set; }

    public static Result<Decision> Record(
        Guid tenantId,
        Guid candidateId,
        string outcome,
        string rationale,
        DateTimeOffset decidedAt)
    {
        if (string.IsNullOrWhiteSpace(rationale))
        {
            return Result.Failure<Decision>(Error.Validation("A rationale is required; an AI suggestion cannot advance the stage alone."));
        }

        if (outcome is not ("advance" or "hold" or "reject"))
        {
            return Result.Failure<Decision>(Error.Validation("Outcome must be advance, hold, or reject."));
        }

        return Result.Success(new Decision
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CandidateId = candidateId,
            Outcome = outcome,
            Rationale = rationale.Trim(),
            DecidedAt = decidedAt
        });
    }
}
