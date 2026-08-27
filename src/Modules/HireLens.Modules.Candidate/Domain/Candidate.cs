using HireLens.SharedKernel;

namespace HireLens.Modules.Candidate.Domain;

public sealed class Candidate : ITenantEntity, ISoftDelete
{
    private Candidate()
    {
        DisplayName = string.Empty;
        Status = "received";
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid PositionId { get; private set; }

    public string DisplayName { get; private set; }

    public string Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public bool IsDeleted { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public static Result<Candidate> Create(Guid tenantId, Guid positionId, string displayName, DateTimeOffset createdAt)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            return Result.Failure<Candidate>(Error.Validation("Display name is required."));
        }

        return Result.Success(new Candidate
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PositionId = positionId,
            DisplayName = displayName.Trim(),
            Status = "received",
            CreatedAt = createdAt
        });
    }

    public void MarkAnalyzing() => Status = "analyzing";

    public void MarkReady() => Status = "ready";

    public void MarkDecided() => Status = "decided";

    public Result SoftDelete(DateTimeOffset deletedAt)
    {
        if (IsDeleted)
        {
            return Result.Success();
        }

        IsDeleted = true;
        DeletedAt = deletedAt;
        return Result.Success();
    }
}
