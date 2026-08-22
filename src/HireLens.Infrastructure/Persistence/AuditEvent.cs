using HireLens.SharedKernel;

namespace HireLens.Infrastructure.Persistence;

public sealed class AuditEvent : ITenantEntity
{
    public Guid Id { get; init; }

    public Guid TenantId { get; init; }

    public required string Action { get; init; }

    public required string EntityType { get; init; }

    public required string EntityId { get; init; }

    public string? ActorSubject { get; init; }

    public DateTimeOffset OccurredAt { get; init; }

    public string? CorrelationId { get; init; }
}
