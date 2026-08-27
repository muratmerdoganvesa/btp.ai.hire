using System.Text.RegularExpressions;
using HireLens.Contracts.Candidates;
using HireLens.Infrastructure.Persistence;
using HireLens.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace HireLens.Modules.Candidate.Application;

public interface ICandidateService
{
    Task<Result<IReadOnlyList<CandidateDto>>> ListAsync(Guid positionId, CancellationToken cancellationToken);

    Task<Result<IReadOnlyList<CandidateBoardItemDto>>> ListBoardAsync(CancellationToken cancellationToken);

    Task<Result<CandidateDto>> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<Result<CandidateDto>> CreateAsync(Guid positionId, CreateCandidateRequest request, CancellationToken cancellationToken);

    Task<Result> SoftDeleteAsync(Guid id, CancellationToken cancellationToken);
}

public sealed class CandidateService(
    HireLensDbContext db,
    ITenantContext tenant,
    ICandidateEvaluationSummaryPort summaries,
    IClock clock) : ICandidateService, ICandidateReadPort, ICandidateWritePort
{
    private static readonly Regex EmailInName = new(
        @"^\s*(?<name>.+?)\s*<\s*(?<email>[^>]+)\s*>\s*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public async Task<Result<IReadOnlyList<CandidateDto>>> ListAsync(Guid positionId, CancellationToken cancellationToken)
    {
        RepositoryGuard.RequireTenant(tenant);
        var rows = await db.Set<Domain.Candidate>()
            .Where(c => c.PositionId == positionId)
            .ToListAsync(cancellationToken);
        var summaryMap = await summaries.GetForCandidatesAsync(rows.Select(c => c.Id).ToList(), cancellationToken);

        return Result.Success<IReadOnlyList<CandidateDto>>(
            rows.Select(c => ToDto(c, summaryMap.GetValueOrDefault(c.Id))).ToList());
    }

    public async Task<Result<IReadOnlyList<CandidateBoardItemDto>>> ListBoardAsync(CancellationToken cancellationToken)
    {
        RepositoryGuard.RequireTenant(tenant);
        var rows = await db.Set<Domain.Candidate>()
            .OrderByDescending(c => c.CreatedAt)
            .Take(500)
            .ToListAsync(cancellationToken);

        if (rows.Count == 0)
        {
            return Result.Success<IReadOnlyList<CandidateBoardItemDto>>([]);
        }

        var ids = rows.Select(c => c.Id).ToList();
        var positionIds = rows.Select(c => c.PositionId).Distinct().ToList();
        var summaryMap = await summaries.GetForCandidatesAsync(ids, cancellationToken);
        var titles = await LoadPositionTitlesAsync(positionIds, cancellationToken);
        var decisions = await LoadLatestDecisionsAsync(ids, cancellationToken);
        var interviews = await LoadInterviewStatusesAsync(ids, cancellationToken);

        var personKeys = rows.ToDictionary(c => c.Id, c => BuildPersonKey(c.DisplayName));
        var siblingCounts = personKeys.Values
            .GroupBy(k => k)
            .ToDictionary(g => g.Key, g => g.Count());

        var board = rows.Select(c =>
        {
            var summary = summaryMap.GetValueOrDefault(c.Id);
            var personKey = personKeys[c.Id];
            var stage = ResolvePipelineStage(
                c.Status,
                summary?.RecommendedAction,
                summary?.EvaluationStatus,
                decisions.GetValueOrDefault(c.Id),
                interviews.GetValueOrDefault(c.Id));

            return new CandidateBoardItemDto(
                c.Id,
                c.PositionId,
                titles.GetValueOrDefault(c.PositionId) ?? "—",
                DisplayNameOnly(c.DisplayName),
                personKey,
                siblingCounts.GetValueOrDefault(personKey, 1),
                ScoreLabel(summary?.OverallScore),
                summary?.OverallScore,
                c.Status,
                stage,
                summary?.RecommendedAction,
                c.CreatedAt);
        }).ToList();

        return Result.Success<IReadOnlyList<CandidateBoardItemDto>>(board);
    }

    public async Task<Result<CandidateDto>> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        RepositoryGuard.RequireTenant(tenant);
        var row = await db.Set<Domain.Candidate>().SingleOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (row is null)
        {
            return Result.Failure<CandidateDto>(Error.NotFound("Candidate was not found."));
        }

        var summaryMap = await summaries.GetForCandidatesAsync([id], cancellationToken);
        return Result.Success(ToDto(row, summaryMap.GetValueOrDefault(id)));
    }

    public async Task<Result<CandidateDto>> CreateAsync(
        Guid positionId,
        CreateCandidateRequest request,
        CancellationToken cancellationToken)
    {
        RepositoryGuard.RequireTenant(tenant);
        var created = Domain.Candidate.Create(tenant.TenantId, positionId, request.DisplayName, clock.UtcNow);
        if (created.IsFailure)
        {
            return Result.Failure<CandidateDto>(created.Error);
        }

        db.Set<Domain.Candidate>().Add(created.Value);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success(ToDto(created.Value, null));
    }

    public async Task<Result> SoftDeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        RepositoryGuard.RequireTenant(tenant);
        var row = await db.Set<Domain.Candidate>().SingleOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (row is null)
        {
            return Result.Failure(Error.NotFound("Candidate was not found."));
        }

        var now = clock.UtcNow;
        row.SoftDelete(now);

        if (!db.Database.IsInMemory())
        {
            await db.Database.ExecuteSqlRawAsync(
                """
                UPDATE "InterviewSessions"
                SET "IsDeleted" = TRUE, "DeletedAt" = {0}, "Status" = CASE WHEN "Status" IN ('completed', 'cancelled') THEN "Status" ELSE 'cancelled' END
                WHERE "TenantId" = {1} AND "CandidateId" = {2} AND ("IsDeleted" = FALSE OR "IsDeleted" IS NULL)
                """,
                [now, tenant.TenantId, id],
                cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    async Task<CandidateSnapshot?> ICandidateReadPort.GetAsync(Guid candidateId, CancellationToken cancellationToken)
    {
        var result = await GetAsync(candidateId, cancellationToken);
        return result.IsFailure
            ? null
            : new CandidateSnapshot(result.Value.Id, result.Value.PositionId, result.Value.DisplayName);
    }

    private async Task<Dictionary<Guid, string>> LoadPositionTitlesAsync(
        IReadOnlyList<Guid> positionIds,
        CancellationToken cancellationToken)
    {
        if (positionIds.Count == 0 || db.Database.IsInMemory())
        {
            return [];
        }

        try
        {
            var rows = await db.Database
                .SqlQueryRaw<IdTitleRow>(
                    """
                    SELECT "Id", "Title"
                    FROM "Positions"
                    WHERE "TenantId" = {0}
                    """,
                    tenant.TenantId)
                .ToListAsync(cancellationToken);
            var wanted = positionIds.ToHashSet();
            return rows
                .Where(r => wanted.Contains(r.Id))
                .ToDictionary(r => r.Id, r => r.Title);
        }
        catch
        {
            return [];
        }
    }

    private async Task<Dictionary<Guid, string>> LoadLatestDecisionsAsync(
        IReadOnlyList<Guid> candidateIds,
        CancellationToken cancellationToken)
    {
        if (candidateIds.Count == 0 || db.Database.IsInMemory())
        {
            return [];
        }

        try
        {
            var rows = await db.Database
                .SqlQueryRaw<DecisionHintRow>(
                    """
                    SELECT "CandidateId", "Outcome", "DecidedAt"
                    FROM "Decisions"
                    WHERE "TenantId" = {0}
                    """,
                    tenant.TenantId)
                .ToListAsync(cancellationToken);
            var wanted = candidateIds.ToHashSet();
            return rows
                .Where(r => wanted.Contains(r.CandidateId))
                .GroupBy(r => r.CandidateId)
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderByDescending(x => x.DecidedAt).First().Outcome);
        }
        catch
        {
            return [];
        }
    }

    private async Task<Dictionary<Guid, string>> LoadInterviewStatusesAsync(
        IReadOnlyList<Guid> candidateIds,
        CancellationToken cancellationToken)
    {
        if (candidateIds.Count == 0 || db.Database.IsInMemory())
        {
            return [];
        }

        try
        {
            var rows = await db.Database
                .SqlQueryRaw<InterviewHintRow>(
                    """
                    SELECT "CandidateId", "Status"
                    FROM "InterviewSessions"
                    WHERE "TenantId" = {0} AND ("IsDeleted" = FALSE OR "IsDeleted" IS NULL)
                    """,
                    tenant.TenantId)
                .ToListAsync(cancellationToken);
            var wanted = candidateIds.ToHashSet();
            return rows
                .Where(r => wanted.Contains(r.CandidateId))
                .GroupBy(r => r.CandidateId)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(x => x.Status).FirstOrDefault(s => s is "completed" or "started" or "invited") ?? g.First().Status);
        }
        catch
        {
            return [];
        }
    }

    private static string ResolvePipelineStage(
        string status,
        string? recommendedAction,
        string? evaluationStatus,
        string? decisionOutcome,
        string? interviewStatus)
    {
        if (string.Equals(decisionOutcome, "reject", StringComparison.OrdinalIgnoreCase))
        {
            return "rejected";
        }

        if (string.Equals(decisionOutcome, "advance", StringComparison.OrdinalIgnoreCase))
        {
            return "offer";
        }

        if (string.Equals(decisionOutcome, "hold", StringComparison.OrdinalIgnoreCase))
        {
            return "hold";
        }

        if (string.Equals(interviewStatus, "completed", StringComparison.OrdinalIgnoreCase))
        {
            return "interview";
        }

        if (interviewStatus is "invited" or "started" or "paused")
        {
            return "pre_interview";
        }

        if (string.Equals(status, "decided", StringComparison.OrdinalIgnoreCase))
        {
            return "reviewing";
        }

        if (string.Equals(recommendedAction, "processing", StringComparison.OrdinalIgnoreCase)
            || string.Equals(evaluationStatus, "processing", StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, "analyzing", StringComparison.OrdinalIgnoreCase))
        {
            return "reviewing";
        }

        if (string.Equals(status, "ready", StringComparison.OrdinalIgnoreCase)
            || string.Equals(recommendedAction, "shortlist", StringComparison.OrdinalIgnoreCase)
            || string.Equals(recommendedAction, "review", StringComparison.OrdinalIgnoreCase))
        {
            return "reviewing";
        }

        if (string.Equals(status, "received", StringComparison.OrdinalIgnoreCase))
        {
            return "new";
        }

        return "pool";
    }

    private static string BuildPersonKey(string displayName)
    {
        var match = EmailInName.Match(displayName);
        if (match.Success)
        {
            return match.Groups["email"].Value.Trim().ToLowerInvariant();
        }

        return string.Join(
            ' ',
            displayName.Trim().ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static string DisplayNameOnly(string displayName)
    {
        var match = EmailInName.Match(displayName);
        return match.Success ? match.Groups["name"].Value.Trim() : displayName.Trim();
    }

    private static CandidateDto ToDto(Domain.Candidate candidate, CandidateEvaluationSummary? summary)
    {
        var score = summary?.OverallScore;
        return new CandidateDto(
            candidate.Id,
            candidate.PositionId,
            candidate.DisplayName,
            ScoreLabel(score),
            score,
            candidate.Status,
            candidate.CreatedAt,
            summary?.CoverageRatio,
            summary?.RecommendedAction,
            summary?.EvaluationStatus,
            summary?.RiskFlagCount ?? 0);
    }

    private static string? ScoreLabel(int? score) =>
        score switch
        {
            >= 75 => "strong",
            >= 60 => "moderate",
            null => null,
            _ => "limited"
        };

    private sealed class IdTitleRow
    {
        public Guid Id { get; set; }

        public string Title { get; set; } = string.Empty;
    }

    private sealed class DecisionHintRow
    {
        public Guid CandidateId { get; set; }

        public string Outcome { get; set; } = string.Empty;

        public DateTimeOffset DecidedAt { get; set; }
    }

    private sealed class InterviewHintRow
    {
        public Guid CandidateId { get; set; }

        public string Status { get; set; } = string.Empty;
    }
}
