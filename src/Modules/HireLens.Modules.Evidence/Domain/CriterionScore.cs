using HireLens.Contracts.Evidence;
using HireLens.SharedKernel;

namespace HireLens.Modules.Evidence.Domain;

public sealed class CriterionScore : ITenantEntity
{
    private readonly List<EvidenceItem> _evidence = [];

    private CriterionScore()
    {
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid EvaluationId { get; private set; }

    public Guid CriterionId { get; private set; }

    public int? Score { get; private set; }

    public int Weight { get; private set; }

    public double Confidence { get; private set; }

    public EvidenceStatus EvidenceStatus { get; private set; }

    public IReadOnlyCollection<EvidenceItem> Evidence => _evidence;

    /// <summary>
    /// A numeric score is admitted only with at least one evidence quote.
    /// HANA cannot enforce this as a CHECK, so the factory is the gate.
    /// </summary>
    public static CriterionScore Create(
        Guid tenantId,
        Guid evaluationId,
        Guid criterionId,
        int weight,
        int? score,
        double confidence,
        IReadOnlyList<EvidenceDraft> evidence)
    {
        Guard.NotEmpty(tenantId, nameof(tenantId));
        Guard.NotEmpty(evaluationId, nameof(evaluationId));
        Guard.NotEmpty(criterionId, nameof(criterionId));

        if (score is not null && evidence.Count == 0)
        {
            throw new DomainException("A numeric score cannot be assigned without evidence.");
        }

        var row = new CriterionScore
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            EvaluationId = evaluationId,
            CriterionId = criterionId,
            Weight = weight,
            Confidence = confidence,
            Score = evidence.Count == 0 ? null : score,
            EvidenceStatus = evidence.Count == 0 ? EvidenceStatus.Insufficient : EvidenceStatus.Sufficient
        };

        foreach (var draft in evidence)
        {
            row._evidence.Add(EvidenceItem.Create(tenantId, row.Id, draft));
        }

        return row;
    }
}

public sealed record EvidenceDraft(string Source, string Quote, int StartOffset, int EndOffset);

public sealed class EvidenceItem : ITenantEntity
{
    private EvidenceItem()
    {
        Source = string.Empty;
        Quote = string.Empty;
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid CriterionScoreId { get; private set; }

    public string Source { get; private set; }

    public string Quote { get; private set; }

    public int StartOffset { get; private set; }

    public int EndOffset { get; private set; }

    public static EvidenceItem Create(Guid tenantId, Guid scoreId, EvidenceDraft draft) =>
        new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CriterionScoreId = scoreId,
            Source = Guard.NotNullOrWhiteSpace(draft.Source, nameof(draft.Source)),
            Quote = Guard.NotNullOrWhiteSpace(draft.Quote, nameof(draft.Quote)),
            StartOffset = draft.StartOffset,
            EndOffset = draft.EndOffset
        };
}
