using HireLens.SharedKernel;

namespace HireLens.Modules.Matching.Domain;

public sealed class Evaluation : ITenantEntity
{
    private Evaluation()
    {
        Status = "queued";
        PromptVersion = string.Empty;
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid PositionId { get; private set; }

    public Guid CandidateId { get; private set; }

    public Guid DocumentId { get; private set; }

    public int? OverallScore { get; private set; }

    public int? CvScore { get; private set; }

    public int? InterviewScore { get; private set; }

    public string Status { get; private set; }

    public string PromptVersion { get; private set; }

    public string? Summary { get; private set; }

    public string? FollowUpsJson { get; private set; }

    public string? NeedsVerificationJson { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

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
            Status = "queued",
            PromptVersion = "pending",
            CreatedAt = createdAt
        };

    public void Complete(
        int? overallScore,
        string promptVersion,
        string? summary,
        IReadOnlyList<string> followUps,
        IReadOnlyList<string> needsVerification)
    {
        CvScore = overallScore;
        OverallScore = overallScore;
        PromptVersion = promptVersion;
        Summary = summary;
        FollowUpsJson = string.Join('\n', followUps);
        NeedsVerificationJson = string.Join('\n', needsVerification);
        Status = "completed";
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
