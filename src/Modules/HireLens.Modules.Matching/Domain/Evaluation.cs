using HireLens.SharedKernel;

namespace HireLens.Modules.Matching.Domain;

public sealed class Evaluation : ITenantEntity
{
    private Evaluation()
    {
        Status = "pending";
        PromptVersion = string.Empty;
        RubricVersion = string.Empty;
        ModelName = string.Empty;
        ModelVersion = string.Empty;
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid PositionId { get; private set; }

    public Guid CandidateId { get; private set; }

    public Guid DocumentId { get; private set; }

    public int? OverallScore { get; private set; }

    public int? CvScore { get; private set; }

    public int? InterviewScore { get; private set; }

    /// <summary>Fraction of rubric weight that had evidence (0–1).</summary>
    public decimal CoverageRatio { get; private set; }

    public string Status { get; private set; }

    public string PromptVersion { get; private set; }

    public string RubricVersion { get; private set; }

    public string ModelName { get; private set; }

    public string ModelVersion { get; private set; }

    public string? FailureStage { get; private set; }

    public string? FailureMessage { get; private set; }

    public string? Summary { get; private set; }

    public string? FollowUpsJson { get; private set; }

    public string? NeedsVerificationJson { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? ExecutedAt { get; private set; }

    public static Evaluation Start(
        Guid tenantId,
        Guid positionId,
        Guid candidateId,
        Guid documentId,
        DateTimeOffset createdAt) =>
        new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PositionId = positionId,
            CandidateId = candidateId,
            DocumentId = documentId,
            Status = "pending",
            PromptVersion = "pending",
            CreatedAt = createdAt
        };

    public void SetStage(string stage) => Status = stage;

    public void Complete(
        int? overallScore,
        decimal coverageRatio,
        string promptVersion,
        string rubricVersion,
        string modelName,
        string modelVersion,
        string? summary,
        IReadOnlyList<string> followUps,
        IReadOnlyList<string> needsVerification,
        DateTimeOffset executedAt)
    {
        CvScore = overallScore;
        OverallScore = overallScore;
        CoverageRatio = coverageRatio;
        PromptVersion = promptVersion;
        RubricVersion = rubricVersion;
        ModelName = modelName;
        ModelVersion = modelVersion;
        Summary = summary;
        FollowUpsJson = string.Join('\n', followUps);
        NeedsVerificationJson = string.Join('\n', needsVerification);
        ExecutedAt = executedAt;
        FailureStage = null;
        FailureMessage = null;
        Status = "completed";
    }

    /// <summary>Backward-compatible overload used by older callers.</summary>
    public void Complete(
        int? overallScore,
        string promptVersion,
        string? summary,
        IReadOnlyList<string> followUps,
        IReadOnlyList<string> needsVerification)
    {
        Complete(
            overallScore,
            coverageRatio: 1m,
            promptVersion,
            rubricVersion: "legacy",
            modelName: "deterministic",
            modelVersion: "1",
            summary,
            followUps,
            needsVerification,
            DateTimeOffset.UtcNow);
    }

    public void Fail(string stage, string message, DateTimeOffset executedAt)
    {
        Status = "failed";
        FailureStage = stage;
        FailureMessage = message;
        ExecutedAt = executedAt;
    }

    public void BlendInterview(int? interviewScore, int interviewWeight)
    {
        InterviewScore = interviewScore;
        if (CvScore is null && interviewScore is null)
        {
            OverallScore = null;
            return;
        }

        var weight = Math.Clamp(interviewWeight, 0, 100);
        var cvPart = CvScore ?? 0;
        var ivPart = interviewScore ?? 0;
        if (CvScore is null)
        {
            OverallScore = interviewScore;
            return;
        }

        if (interviewScore is null)
        {
            OverallScore = CvScore;
            return;
        }

        OverallScore = (int)Math.Round((cvPart * (100 - weight) + ivPart * weight) / 100.0);
    }
}
