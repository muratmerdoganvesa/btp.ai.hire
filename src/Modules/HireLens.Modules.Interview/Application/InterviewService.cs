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

    Task<Result<IReadOnlyList<InterviewBoardItemDto>>> ListBoardAsync(CancellationToken cancellationToken);

    Task<Result<InterviewSessionDto>> GetByIdAsync(Guid sessionId, CancellationToken cancellationToken);

    Task<Result<InterviewPrepDto>> PrepAsync(string token, CancellationToken cancellationToken);

    Task<Result<InterviewSessionDto>> GetByTokenAsync(string token, CancellationToken cancellationToken);

    Task<Result<InterviewSessionDto>> DiscloseAsync(string token, CancellationToken cancellationToken);

    Task<Result<InterviewSessionDto>> StartAsync(string token, CancellationToken cancellationToken);

    Task<Result<InterviewSessionDto>> PauseAsync(string token, CancellationToken cancellationToken);

    Task<Result<InterviewSessionDto>> ResumeAsync(string token, CancellationToken cancellationToken);

    Task<Result<InterviewSessionDto>> AnswerAsync(string token, InterviewAnswerRequest request, CancellationToken cancellationToken);

    Task<Result<InterviewSessionDto>> GetForCandidateAsync(Guid candidateId, CancellationToken cancellationToken);

    Task<Result> SoftDeleteForCandidateAsync(Guid candidateId, CancellationToken cancellationToken);
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

    public async Task<Result<IReadOnlyList<InterviewBoardItemDto>>> ListBoardAsync(CancellationToken cancellationToken)
    {
        RepositoryGuard.RequireTenant(tenant);
        var sessions = await db.Set<InterviewSession>()
            .AsNoTracking()
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(cancellationToken);

        if (sessions.Count == 0)
        {
            return Result.Success<IReadOnlyList<InterviewBoardItemDto>>([]);
        }

        var candidateIds = sessions.Select(s => s.CandidateId).Distinct().ToList();
        var positionIds = sessions.Select(s => s.PositionId).Distinct().ToList();

        var candidateNames = new Dictionary<Guid, string>();
        foreach (var id in candidateIds)
        {
            var snap = await candidates.GetAsync(id, cancellationToken);
            if (snap is not null)
            {
                candidateNames[id] = snap.DisplayName;
            }
        }

        var positionTitles = new Dictionary<Guid, string>();
        foreach (var id in positionIds)
        {
            var snap = await positions.GetAsync(id, cancellationToken);
            if (snap is not null)
            {
                positionTitles[id] = snap.Title;
            }
        }

        var sessionIds = sessions.Select(s => s.Id).ToList();
        var questionCounts = await db.Set<InterviewQuestion>()
            .AsNoTracking()
            .Where(q => sessionIds.Contains(q.SessionId))
            .GroupBy(q => q.SessionId)
            .Select(g => new { SessionId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.SessionId, x => x.Count, cancellationToken);
        var answerCounts = await db.Set<InterviewTurn>()
            .AsNoTracking()
            .Where(t => sessionIds.Contains(t.SessionId) && t.Role == "candidate")
            .GroupBy(t => t.SessionId)
            .Select(g => new { SessionId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.SessionId, x => x.Count, cancellationToken);

        IReadOnlyList<InterviewBoardItemDto> rows = sessions
            .Select(s => new InterviewBoardItemDto(
                s.Id,
                s.CandidateId,
                candidateNames.GetValueOrDefault(s.CandidateId, "—"),
                s.PositionId,
                positionTitles.GetValueOrDefault(s.PositionId, "—"),
                s.Status,
                s.InterviewScore,
                questionCounts.GetValueOrDefault(s.Id),
                answerCounts.GetValueOrDefault(s.Id),
                s.CreatedAt,
                s.ExpiresAt))
            .ToList();
        return Result.Success(rows);
    }

    public async Task<Result<InterviewSessionDto>> GetByIdAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        RepositoryGuard.RequireTenant(tenant);
        var session = await db.Set<InterviewSession>()
            .AsNoTracking()
            .SingleOrDefaultAsync(s => s.Id == sessionId, cancellationToken);
        if (session is null)
        {
            return Result.Failure<InterviewSessionDto>(Error.NotFound("Interview was not found."));
        }

        var frames = await db.Set<InterviewFrame>()
            .AsNoTracking()
            .Where(f => f.SessionId == session.Id)
            .OrderBy(f => f.CapturedAt)
            .ToListAsync(cancellationToken);

        var candidate = await candidates.GetAsync(session.CandidateId, cancellationToken);
        var position = await positions.GetAsync(session.PositionId, cancellationToken);
        return Result.Success(ToDto(session, frames, candidate?.DisplayName, position?.Title));
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

        var session = opened.Value;

        // Detach interview graph so consent SaveChanges cannot flush InterviewSession+AutoInclude.
        db.ChangeTracker.Clear();

        await privacy.GrantAsync(session.CandidateId, DisclosurePurpose, cancellationToken);

        if (!session.DisclosureAccepted)
        {
            await MarkSessionDisclosedAsync(session.Id, cancellationToken);
            session.AcceptDisclosure();
        }

        return Result.Success(ToDto(session));
    }

    public async Task<Result<InterviewSessionDto>> StartAsync(string token, CancellationToken cancellationToken)
    {
        var opened = await OpenAsync(token, cancellationToken);
        if (opened.IsFailure)
        {
            return Result.Failure<InterviewSessionDto>(opened.Error);
        }

        var session = opened.Value;
        if (!session.DisclosureAccepted
            && !await privacy.HasAsync(session.CandidateId, DisclosurePurpose, cancellationToken))
        {
            return Result.Failure<InterviewSessionDto>(Error.Validation("AI disclosure consent is required."));
        }

        if (!session.DisclosureAccepted)
        {
            await MarkSessionDisclosedAsync(session.Id, cancellationToken);
            session.AcceptDisclosure();
        }

        if (session.Questions.Count == 0)
        {
            await SeedQuestionsAsync(session, cancellationToken);
        }

        if (session.Questions.Count == 0)
        {
            session.AddQuestion(
                Guid.Parse("11111111-1111-1111-1111-111111111111"),
                "Bu rol için somut bir başarı veya deneyiminizi anlatın.",
                1);
        }

        var started = session.Start();
        if (started.IsFailure)
        {
            return Result.Failure<InterviewSessionDto>(started.Error);
        }

        var first = session.Questions.OrderBy(q => q.Order).FirstOrDefault();
        InterviewTurn? openingTurn = null;
        if (first is not null && session.Turns.Count == 0)
        {
            openingTurn = session.AddTurn("assistant", first.Prompt, first.Id, clock.UtcNow);
        }

        // Persist children without a tracked InterviewSession parent (HANA AutoInclude SaveChanges bug).
        db.ChangeTracker.Clear();

        if (UsesRelationalSql())
        {
            foreach (var question in session.Questions)
            {
                await InsertQuestionRawAsync(question, cancellationToken);
            }

            if (openingTurn is not null)
            {
                await InsertTurnRawAsync(openingTurn, cancellationToken);
            }

            await db.Database.ExecuteSqlInterpolatedAsync(
                $"""
                UPDATE "InterviewSessions"
                SET "Status" = {"in_progress"}
                WHERE "Id" = {session.Id.ToString("D")}
                """,
                cancellationToken);
        }
        else
        {
            foreach (var question in session.Questions)
            {
                db.Set<InterviewQuestion>().Add(question);
            }

            if (openingTurn is not null)
            {
                db.Set<InterviewTurn>().Add(openingTurn);
            }

            await db.SaveChangesAsync(cancellationToken);
            var tracked = await db.Set<InterviewSession>()
                .IgnoreAutoIncludes()
                .SingleAsync(s => s.Id == session.Id, cancellationToken);
            if (!tracked.DisclosureAccepted)
            {
                tracked.AcceptDisclosure();
            }

            var status = tracked.Start();
            if (status.IsFailure)
            {
                return Result.Failure<InterviewSessionDto>(status.Error);
            }

            await db.SaveChangesAsync(cancellationToken);
        }

        return Result.Success(ToDto(session));
    }

    private async Task MarkSessionDisclosedAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        if (UsesRelationalSql())
        {
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"""
                UPDATE "InterviewSessions"
                SET "DisclosureAccepted" = TRUE, "Status" = {"disclosed"}
                WHERE "Id" = {sessionId.ToString("D")}
                """,
                cancellationToken);
            return;
        }

        // EF InMemory has no ExecuteUpdate — update scalars only (no AutoInclude graph).
        var tracked = await db.Set<InterviewSession>()
            .IgnoreAutoIncludes()
            .SingleAsync(s => s.Id == sessionId, cancellationToken);
        tracked.AcceptDisclosure();
        await db.SaveChangesAsync(cancellationToken);
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

        db.Entry(session).State = EntityState.Detached;
        var candidateTurn = session.AddTurn("candidate", request.Text.Trim(), current?.Id, clock.UtcNow);
        db.Set<InterviewTurn>().Add(candidateTurn);

        foreach (var frame in BuildFrames(session, candidateTurn, current?.Id, request.FramesBase64))
        {
            db.Set<InterviewFrame>().Add(frame);
        }

        var unanswered = session.Questions.OrderBy(q => q.Order)
            .FirstOrDefault(q => session.Turns.Count(t => t.QuestionId == q.Id && t.Role == "candidate") == 0);
        if (unanswered is not null)
        {
            var next = session.AddTurn("assistant", unanswered.Prompt, unanswered.Id, clock.UtcNow);
            db.Set<InterviewTurn>().Add(next);
            await db.SaveChangesAsync(cancellationToken);
            return Result.Success(ToDto(session));
        }

        await db.SaveChangesAsync(cancellationToken);

        try
        {
            await EvaluateAsync(session, cancellationToken);
        }
        catch
        {
            session.Complete(null, "Interview finished; scoring will be reviewed by the recruiter.");
        }

        if (UsesRelationalSql())
        {
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"""
                UPDATE "InterviewSessions"
                SET "Status" = {session.Status},
                    "InterviewScore" = {session.InterviewScore},
                    "Summary" = {session.Summary}
                WHERE "Id" = {session.Id.ToString("D")}
                """,
                cancellationToken);
        }
        else
        {
            var tracked = await db.Set<InterviewSession>().SingleAsync(s => s.Id == session.Id, cancellationToken);
            tracked.Complete(session.InterviewScore, session.Summary);
            await db.SaveChangesAsync(cancellationToken);
        }

        return Result.Success(ToDto(session));
    }

    public async Task<Result<InterviewSessionDto>> GetForCandidateAsync(Guid candidateId, CancellationToken cancellationToken)
    {
        RepositoryGuard.RequireTenant(tenant);
        var session = await db.Set<InterviewSession>()
            .Where(s => s.CandidateId == candidateId)
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (session is null)
        {
            return Result.Failure<InterviewSessionDto>(Error.NotFound("Interview was not found."));
        }

        var frames = await db.Set<InterviewFrame>()
            .Where(f => f.SessionId == session.Id)
            .OrderBy(f => f.CapturedAt)
            .ToListAsync(cancellationToken);
        return Result.Success(ToDto(session, frames));
    }

    public async Task<Result> SoftDeleteForCandidateAsync(Guid candidateId, CancellationToken cancellationToken)
    {
        RepositoryGuard.RequireTenant(tenant);
        var sessions = await db.Set<InterviewSession>()
            .Where(s => s.CandidateId == candidateId)
            .ToListAsync(cancellationToken);
        if (sessions.Count == 0)
        {
            return Result.Failure(Error.NotFound("Interview was not found."));
        }

        var now = clock.UtcNow;
        foreach (var session in sessions)
        {
            session.SoftDelete(now);
        }

        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
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
        // Deterministic only — do not call the AI gateway here. Gateway SaveChanges /
        // orchestration failures were surfacing as opaque "Sequence contains no elements"
        // on the public start endpoint.
        IReadOnlyList<CriterionScoreDto> gaps = [];
        try
        {
            var evaluation = await evaluations.GetForCandidateAsync(session.CandidateId, cancellationToken);
            gaps = evaluation?.Scores.Where(s => s.Score is null).ToList() ?? [];
        }
        catch
        {
            gaps = [];
        }

        PositionSnapshot? position = null;
        try
        {
            position = await positions.GetAsync(session.PositionId, cancellationToken);
        }
        catch
        {
            position = null;
        }

        if (gaps.Count == 0 && position is not null)
        {
            gaps = position.Criteria
                .Select(c => new CriterionScoreDto(c.Id, c.Name, null, c.Weight, 0, EvidenceStatus.Insufficient, []))
                .ToList();
        }

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

    private bool UsesRelationalSql() => !db.Database.IsInMemory();

    private async Task InsertQuestionRawAsync(InterviewQuestion question, CancellationToken cancellationToken)
    {
        // Prefer QuestionOrder; fall back to legacy "Order" column if ALTER has not run yet.
        try
        {
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"""
                UPSERT "InterviewQuestions"
                ("Id","TenantId","SessionId","CriterionId","Prompt","QuestionOrder")
                VALUES (
                    {question.Id.ToString("D")},
                    {question.TenantId.ToString("D")},
                    {question.SessionId.ToString("D")},
                    {question.CriterionId.ToString("D")},
                    {question.Prompt},
                    {question.Order}
                )
                WITH PRIMARY KEY
                """,
                cancellationToken);
        }
        catch
        {
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"""
                UPSERT "InterviewQuestions"
                ("Id","TenantId","SessionId","CriterionId","Prompt","Order")
                VALUES (
                    {question.Id.ToString("D")},
                    {question.TenantId.ToString("D")},
                    {question.SessionId.ToString("D")},
                    {question.CriterionId.ToString("D")},
                    {question.Prompt},
                    {question.Order}
                )
                WITH PRIMARY KEY
                """,
                cancellationToken);
        }
    }

    private async Task InsertTurnRawAsync(InterviewTurn turn, CancellationToken cancellationToken)
    {
        var questionId = turn.QuestionId is Guid qid ? qid.ToString("D") : null;
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            UPSERT "InterviewTurns"
            ("Id","TenantId","SessionId","QuestionId","Role","Text","CreatedAt")
            VALUES (
                {turn.Id.ToString("D")},
                {turn.TenantId.ToString("D")},
                {turn.SessionId.ToString("D")},
                {questionId},
                {turn.Role},
                {turn.Text},
                {turn.CreatedAt.ToString("O")}
            )
            WITH PRIMARY KEY
            """,
            cancellationToken);
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

    private static InterviewSessionDto ToDto(
        InterviewSession session,
        IReadOnlyList<InterviewFrame>? frames = null,
        string? candidateName = null,
        string? positionTitle = null) =>
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
            session.ExpiresAt,
            frames?.Select(f => new InterviewFrameDto(
                f.Id,
                f.QuestionId,
                f.TurnId,
                f.ContentType,
                f.ImageBase64,
                f.CapturedAt)).ToList(),
            candidateName,
            positionTitle,
            session.CreatedAt);

    private List<InterviewFrame> BuildFrames(
        InterviewSession session,
        InterviewTurn turn,
        Guid? questionId,
        IReadOnlyList<string>? framesBase64)
    {
        if (framesBase64 is null || framesBase64.Count == 0)
        {
            return [];
        }

        const int maxFrames = 3;
        const int maxBase64Chars = 220_000;
        var result = new List<InterviewFrame>();
        foreach (var raw in framesBase64.Take(maxFrames))
        {
            if (!TryNormalizeFrame(raw, out var contentType, out var base64))
            {
                continue;
            }

            if (base64.Length > maxBase64Chars)
            {
                continue;
            }

            result.Add(InterviewFrame.Create(
                session.TenantId,
                session.Id,
                session.CandidateId,
                session.PositionId,
                contentType,
                base64,
                questionId,
                turn.Id,
                clock.UtcNow));
        }

        return result;
    }

    private static bool TryNormalizeFrame(string? raw, out string contentType, out string base64)
    {
        contentType = "image/jpeg";
        base64 = string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var value = raw.Trim();
        if (value.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            var comma = value.IndexOf(',');
            if (comma <= 0)
            {
                return false;
            }

            var header = value[..comma];
            base64 = value[(comma + 1)..].Trim();
            if (header.Contains("image/png", StringComparison.OrdinalIgnoreCase))
            {
                contentType = "image/png";
            }
            else if (header.Contains("image/jpeg", StringComparison.OrdinalIgnoreCase)
                     || header.Contains("image/jpg", StringComparison.OrdinalIgnoreCase))
            {
                contentType = "image/jpeg";
            }
            else
            {
                return false;
            }
        }
        else
        {
            base64 = value;
        }

        // Cheap sanity: base64 alphabet only (ignore whitespace already trimmed).
        return base64.Length >= 32
               && base64.All(c => char.IsLetterOrDigit(c) || c is '+' or '/' or '=' or '\r' or '\n');
    }

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
