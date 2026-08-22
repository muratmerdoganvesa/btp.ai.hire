using HireLens.SharedKernel;

namespace HireLens.Infrastructure.Persistence;

public sealed class AiInvocation : ITenantEntity
{
    public Guid Id { get; init; }

    public Guid TenantId { get; init; }

    public required string TaskType { get; init; }

    public required string ModelId { get; init; }

    public required string PromptVersion { get; init; }

    public required string PromptHash { get; init; }

    public int InputTokens { get; init; }

    public int OutputTokens { get; init; }

    public decimal EstimatedCost { get; init; }

    public long LatencyMs { get; init; }

    public double? Confidence { get; init; }

    public string? CorrelationId { get; init; }

    public DateTimeOffset OccurredAt { get; init; }
}
