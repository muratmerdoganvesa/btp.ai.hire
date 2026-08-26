using HireLens.AiGateway;
using HireLens.Contracts.Candidates;
using HireLens.Contracts.Configuration;
using HireLens.Contracts.Evidence;
using HireLens.Contracts.Interview;
using HireLens.Contracts.Matching;
using HireLens.Contracts.Notifications;
using HireLens.Contracts.Privacy;
using HireLens.Contracts.Recruiting;
using HireLens.Infrastructure.Persistence;
using HireLens.Modules.Interview.Domain;
using HireLens.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace HireLens.Modules.Interview.Application;

public interface IInterviewService
{
    Task<Result<InterviewInviteDto>> InviteAsync(InterviewInviteRequest request, CancellationToken cancellationToken);

    Task<Result<InterviewPrepDto>> PrepAsync(string token, CancellationToken cancellationToken);

    Task<Result<InterviewSessionDto>> GetByTokenAsync(string token, CancellationToken cancellationToken);

    Task<Result<InterviewSessionDto>> DiscloseAsync(string token, CancellationToken cancellationToken);

    Task<Result<InterviewSessionDto>> StartAsync(string token, CancellationToken cancellationToken);

    Task<Result<InterviewSessionDto>> PauseAsync(string token, CancellationToken cancellationToken);

    Task<Result<InterviewSessionDto>> ResumeAsync(string token, CancellationToken cancellationToken);

    Task<Result<InterviewSessionDto>> AnswerAsync(string token, InterviewAnswerRequest request, CancellationToken cancellationToken);

    Task<Result<InterviewSessionDto>> GetForCandidateAsync(Guid candidateId, CancellationToken cancellationToken);
}

public sealed class InterviewService(
    HireLensDbContext db,
    ITenantContext tenant,
    TenantContext tenantState,
    IClock clock,
    IConfiguration configuration,
    ICandidateReadPort candidates,
    IPositionReadPort positions,
    IEvaluationReadPort evaluations,
    IEvaluationBlendPort blend,
    IEvidenceScoring evidence,
    IPrivacyConsentPort privacy,
    INotificationSink notifications,
    IInterviewWeightPolicy weights,
    IAiGateway gateway,
    InterviewTokenSigner tokens) : IInterviewService
{
    public const string DisclosurePurpose = "ai_interview_disclosure";

    public async Task<Result<InterviewInviteDto>> InviteAsync(InterviewInviteRequest request, CancellationToken cancellationToken)
    {
        RepositoryGuard.RequireTenant(tenant);
        var candidate = await candidates.GetAsync(request.CandidateId, cancellationToken);
        if (candidate is null)
        {
            return Result.Failure<InterviewInviteDto>(Error.NotFound("Candidate was not found."));
        }

        var session = InterviewSession.Invite(
            tenant.TenantId,
            request.CandidateId,
            request.PositionId,
            "pending",
            clock.UtcNow);
        var token = tokens.Issue(tenant.TenantId, session.Id);
        session.BindToken(tokens.Hash(token));
        db.Set<InterviewSession>().Add(session);
        await db.SaveChangesAsync(cancellationToken);

        var url = BuildInviteUrl(token);
        await notifications.SendAsync(
            new NotificationDraft(
                request.CandidateId,
                "email",
                "HireLens interview invitation",
                "A signed video interview link was issued (3 camera answers → transcript).",
                url),
            cancellationToken);

        return Result.Success(new InterviewInviteDto(session.Id, url, session.ExpiresAt, session.VideoMeetingUrl));
    }

    public async Task<Result<InterviewPrepDto>> PrepAsync(string token, CancellationToken cancellationToken)
    {
        var opened = await OpenAsync(token, cancellationToken);
        if (opened.IsFailure)
        {
            return Result.Failure<InterviewPrepDto>(opened.Error);
        }

        return Result.Success(new InterviewPrepDto(
            "Up to 3 questions. Open your camera, speak your answer; HireLens converts speech to text for evidence scoring. No face or emotion analysis.",
            15,
            "Spoken answers become transcript text. Video is captured in your browser for answering; scoring uses text quotes only.",
            true,
            null,
            opened.Value.ExpiresAt));
    }

    public async Task<Result<InterviewSessionDto>> GetByTokenAsync(string token, CancellationToken cancellationToken)
    {
        var opened = await OpenAsync(token, cancellationToken);
        return opened.IsFailure ? Result.Failure<InterviewSessionDto>(opened.Error) : Result.Success(ToDto(opened.Value));
    }

    public async Task<Result<InterviewSessionDto>> DiscloseAsync(string token, CancellationToken cancellationToken)
    {
        var opened = await OpenAsync(token, cancellationToken);
        if (opened.IsFailure)
        {
            return Result.Failure<InterviewSessionDto>(opened.Error);
        }

        opened.Value.AcceptDisclosure();
        await privacy.GrantAsync(opened.Value.CandidateId, DisclosurePurpose, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success(ToDto(opened.Value));
    }

    public async Task<Result<InterviewSessionDto>> StartAsync(string token, CancellationToken cancellationToken)
    {
        var opened = await OpenAsync(token, cancellationToken);
        if (opened.IsFailure)
        {
            return Result.Failure<InterviewSessionDto>(opened.Error);
        }

        var session = opened.Value;
        if (!await privacy.HasAsync(session.CandidateId, DisclosurePurpose, cancellationToken))
        {
            return Result.Failure<InterviewSessionDto>(Error.Validation("AI disclosure consent is required."));
        }

        if (session.Questions.Count == 0)
        {
            await SeedQuestionsAsync(session, cancellationToken);
            foreach (var question in session.Questions)
            {
                EnsureAdded(question);
            }
        }

        var started = session.Start();
        if (started.IsFailure)
        {
            return Result.Failure<InterviewSessionDto>(started.Error);
        }

        var first = session.Questions.OrderBy(q => q.Order).FirstOrDefault();
        if (first is not null && session.Turns.Count == 0)
        {
            EnsureAdded(session.AddTurn("assistant", first.Prompt, first.Id, clock.UtcNow));
        }

        await db.SaveChangesAsync(cancellationToken);
        return Result.Success(ToDto(session));
    }

    public async Task<Result<InterviewSessionDto>> PauseAsync(string token, CancellationToken cancellationToken)
    {
        var opened = await OpenAsync(token, cancellationToken);
        if (opened.IsFailure)
        {
            return Result.Failure<InterviewSessionDto>(opened.Error);
        }

        var paused = opened.Value.Pause();
        if (paused.IsFailure)
        {
            return Result.Failure<InterviewSessionDto>(paused.Error);
        }

        await db.SaveChangesAsync(cancellationToken);
        return Result.Success(ToDto(opened.Value));
    }

    public async Task<Result<InterviewSessionDto>> ResumeAsync(string token, CancellationToken cancellationToken)
    {
        var opened = await OpenAsync(token, cancellationToken);
        if (opened.IsFailure)
        {
            return Result.Failure<InterviewSessionDto>(opened.Error);
        }

        var resumed = opened.Value.Resume();
        if (resumed.IsFailure)
        {
            return Result.Failure<InterviewSessionDto>(resumed.Error);
        }

        await db.SaveChangesAsync(cancellationToken);
        return Result.Success(ToDto(opened.Value));
    }

    public async Task<Result<InterviewSessionDto>> AnswerAsync(string token, InterviewAnswerRequest request, CancellationToken cancellationToken)
    {
        var opened = await OpenAsync(token, cancellationToken);
        if (opened.IsFailure)
        {
            return Result.Failure<InterviewSessionDto>(opened.Error);
        }

        var session = opened.Value;
        if (session.Status is not "in_progress")
        {
            return Result.Failure<InterviewSessionDto>(Error.Validation("The interview is not active."));
        }

        if (string.IsNullOrWhiteSpace(request.Text))
        {
            return Result.Failure<InterviewSessionDto>(Error.Validation("An answer is required."));
        }

        var current = session.Questions.OrderBy(q => q.Order)
            .FirstOrDefault(q => session.Turns.Count(t => t.QuestionId == q.Id && t.Role == "candidate") == 0);
        EnsureAdded(session.AddTurn("candidate", request.Text.Trim(), current?.Id, clock.UtcNow));
        _ = await gateway.ExecuteAsync<LiveStub>(
            AiTaskType.InterviewLiveTurn,
            new PromptContext(request.Text, "v1"),
            ct: cancellationToken);

        var unanswered = session.Questions.OrderBy(q => q.Order)
            .FirstOrDefault(q => session.Turns.Count(t => t.QuestionId == q.Id && t.Role == "candidate") == 0);
        if (unanswered is not null)
        {
            EnsureAdded(session.AddTurn("assistant", unanswered.Prompt, unanswered.Id, clock.UtcNow));
            await db.SaveChangesAsync(cancellationToken);
            return Result.Success(ToDto(session));
        }

        await EvaluateAsync(session, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success(ToDto(session));
    }

    public async Task<Result<InterviewSessionDto>> GetForCandidateAsync(Guid candidateId, CancellationToken cancellationToken)
    {
        RepositoryGuard.RequireTenant(tenant);
        var session = await db.Set<InterviewSession>()
            .Where(s => s.CandidateId == candidateId)
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        return session is null
            ? Result.Failure<InterviewSessionDto>(Error.NotFound("Interview was not found."))
            : Result.Success(ToDto(session));
    }

    private string BuildInviteUrl(string token)
    {
        var host = configuration["PUBLIC_HOST"]
            ?? configuration["App:PublicHost"]
            ?? configuration["BTP:PublicHost"];
        if (string.IsNullOrWhiteSpace(host))
        {
            return $"/interview/{token}";
        }

        var baseUrl = host.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            ? host.TrimEnd('/')
            : $"https://{host.Trim().TrimEnd('/')}";
        return $"{baseUrl}/interview/{token}";
    }

    private async Task<Result<InterviewSession>> OpenAsync(string token, CancellationToken cancellationToken)
    {
        if (!tokens.TryRead(token, out var tenantId, out var sessionId))
        {
            return Result.Failure<InterviewSession>(Error.NotFound("Interview was not found."));
        }

        tenantState.Resolve(tenantId, "candidate", "interview-token");
        var session = await db.Set<InterviewSession>().SingleOrDefaultAsync(s => s.Id == sessionId, cancellationToken);
        if (session is null || !string.Equals(session.TokenHash, tokens.Hash(token), StringComparison.Ordinal))
        {
            return Result.Failure<InterviewSession>(Error.NotFound("Interview was not found."));
        }

        if (session.ExpiresAt < clock.UtcNow)
        {
            return Result.Failure<InterviewSession>(Error.Validation("The interview link has expired."));
        }

        return Result.Success(session);
    }

    private async Task SeedQuestionsAsync(InterviewSession session, CancellationToken cancellationToken)
    {
        var evaluation = await evaluations.GetForCandidateAsync(session.CandidateId, cancellationToken);
        var position = await positions.GetAsync(session.PositionId, cancellationToken);
        var gaps = evaluation?.Scores.Where(s => s.Score is null).ToList() ?? [];
        if (gaps.Count == 0 && position is not null)
        {
            gaps = position.Criteria
                .Select(c => new CriterionScoreDto(c.Id, c.Name, null, c.Weight, 0, EvidenceStatus.Insufficient, []))
                .ToList();
        }

        _ = await gateway.ExecuteAsync<QuestionStub>(
            AiTaskType.InterviewQuestionGen,
            new PromptContext(string.Join('\n', gaps.Select(g => g.CriterionName)), "v1"),
            ct: cancellationToken);

        var order = 1;
        foreach (var gap in gaps.Take(3))
        {
            session.AddQuestion(gap.CriterionId, $"Please share concrete evidence for {gap.CriterionName}.", order++);
        }

        if (session.Questions.Count == 0 && position is not null)
        {
            foreach (var criterion in position.Criteria.Take(3))
            {
                session.AddQuestion(criterion.Id, $"Please share concrete evidence for {criterion.Name}.", order++);
            }
        }
    }

    private async Task EvaluateAsync(InterviewSession session, CancellationToken cancellationToken)
    {
        var evaluation = await evaluations.GetForCandidateAsync(session.CandidateId, cancellationToken);
        var transcript = string.Join('\n', session.Turns.Select(t => t.Text));
        _ = await gateway.ExecuteAsync<EvalStub>(
            AiTaskType.InterviewEvaluation,
            new PromptContext(transcript, "v1"),
            ct: cancellationToken);

        var proposals = new List<ProposedCriterionScore>();
        foreach (var question in session.Questions)
        {
            var answer = session.Turns.LastOrDefault(t => t.QuestionId == question.Id && t.Role == "candidate")?.Text ?? string.Empty;
            var index = transcript.IndexOf(answer, StringComparison.Ordinal);
            if (string.IsNullOrWhiteSpace(answer) || index < 0)
            {
                proposals.Add(new ProposedCriterionScore(question.CriterionId, 0, null, 0.2, []));
                continue;
            }

            proposals.Add(new ProposedCriterionScore(
                question.CriterionId,
                0,
                75,
                0.7,
                [new ProposedEvidence("interview", answer, index, index + answer.Length)]));
        }

        if (evaluation is not null)
        {
            await evidence.ApplyAsync(evaluation.Id, proposals, cancellationToken);
        }

        var score = DeterministicInterviewScore.Overall(proposals);
        session.Complete(score, score is null
            ? "Interview answers did not yield evidence-bound scores."
            : "Interview scores were merged from quoted transcript spans.");
        var weight = await weights.GetInterviewWeightAsync(cancellationToken);
        await blend.BlendInterviewAsync(session.CandidateId, score, weight, cancellationToken);
    }

    private void EnsureAdded<T>(T entity) where T : class
    {
        var entry = db.Entry(entity);
        if (entry.State is EntityState.Detached)
        {
            db.Set<T>().Add(entity);
            return;
        }

        if (entry.State is EntityState.Modified or EntityState.Unchanged)
        {
            entry.State = EntityState.Added;
        }
    }

    private static InterviewSessionDto ToDto(InterviewSession session) =>
        new(
            session.Id,
            session.CandidateId,
            session.PositionId,
            session.Status,
            session.DisclosureAccepted,
            session.InterviewScore,
            session.Questions.OrderBy(q => q.Order).Select(q => new InterviewQuestionDto(q.Id, q.CriterionId, q.Prompt, q.Order)).ToList(),
            session.Turns.OrderBy(t => t.CreatedAt).Select(t => new InterviewTurnDto(t.Id, t.Role, t.Text, t.QuestionId, t.CreatedAt)).ToList(),
            session.Summary,
            session.VideoMeetingUrl,
            session.ExpiresAt);

    private sealed record LiveStub(string? Status);

    private sealed record QuestionStub(string? Status);

    private sealed record EvalStub(string? Status);
}

public static class DeterministicInterviewScore
{
    public static int? Overall(IReadOnlyList<ProposedCriterionScore> scores)
    {
        var numbered = scores.Where(s => s.Score is not null).Select(s => s.Score!.Value).ToList();
        return numbered.Count == 0 ? null : (int)Math.Round(numbered.Average());
    }
}
