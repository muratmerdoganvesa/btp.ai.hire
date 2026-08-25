using HireLens.Contracts.Candidates;
using HireLens.Infrastructure.Persistence;
using HireLens.Modules.Matching.Domain;
using HireLens.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace HireLens.Modules.Candidate.Application;

public interface ICandidateService
{
    Task<Result<IReadOnlyList<CandidateDto>>> ListAsync(Guid positionId, CancellationToken cancellationToken);

    Task<Result<CandidateDto>> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<Result<CandidateDto>> CreateAsync(Guid positionId, CreateCandidateRequest request, CancellationToken cancellationToken);
}

public sealed class CandidateService(
    HireLensDbContext db,
    ITenantContext tenant,
    IClock clock) : ICandidateService, ICandidateReadPort, ICandidateWritePort
{
    public async Task<Result<IReadOnlyList<CandidateDto>>> ListAsync(Guid positionId, CancellationToken cancellationToken)
    {
        RepositoryGuard.RequireTenant(tenant);
        var rows = await db.Set<Domain.Candidate>()
            .Where(c => c.PositionId == positionId)
            .ToListAsync(cancellationToken);
        var evaluations = await db.Set<Evaluation>()
            .Where(e => e.PositionId == positionId)
            .ToListAsync(cancellationToken);
        var evalByCandidate = evaluations
            .GroupBy(e => e.CandidateId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(e => e.CreatedAt).First());

        return Result.Success<IReadOnlyList<CandidateDto>>(
            rows.Select(c => ToDto(c, evalByCandidate.GetValueOrDefault(c.Id))).ToList());
    }

    public async Task<Result<CandidateDto>> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        RepositoryGuard.RequireTenant(tenant);
        var row = await db.Set<Domain.Candidate>().SingleOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (row is null)
        {
            return Result.Failure<CandidateDto>(Error.NotFound("Candidate was not found."));
        }

        var evaluation = await db.Set<Evaluation>()
            .Where(e => e.CandidateId == id)
            .OrderByDescending(e => e.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        return Result.Success(ToDto(row, evaluation));
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
        return Result.Success(ToDto(created.Value, evaluation: null));
    }

    async Task<CandidateSnapshot?> ICandidateReadPort.GetAsync(Guid candidateId, CancellationToken cancellationToken)
    {
        var result = await GetAsync(candidateId, cancellationToken);
        return result.IsFailure
            ? null
            : new CandidateSnapshot(result.Value.Id, result.Value.PositionId, result.Value.DisplayName);
    }

    private static CandidateDto ToDto(Domain.Candidate candidate, Evaluation? evaluation)
    {
        var score = evaluation?.OverallScore;
        var coverage = evaluation?.CoverageRatio;
        var riskCount = CountRiskFlags(evaluation);
        var recommended = ResolveRecommendedAction(evaluation, score, coverage, riskCount);
        var evalStatus = evaluation?.Status;
        var label = ScoreLabel(score);

        return new CandidateDto(
            candidate.Id,
            candidate.PositionId,
            candidate.DisplayName,
            label,
            score,
            candidate.Status,
            candidate.CreatedAt,
            coverage,
            recommended,
            evalStatus,
            riskCount);
    }

    private static string? ScoreLabel(int? score) =>
        score switch
        {
            >= 75 => "strong",
            >= 60 => "moderate",
            null => null,
            _ => "limited"
        };

    private static int CountRiskFlags(Evaluation? evaluation)
    {
        if (evaluation?.NeedsVerificationJson is not { Length: > 0 } json)
        {
            return 0;
        }

        return json.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Length;
    }

    private static string? ResolveRecommendedAction(
        Evaluation? evaluation,
        int? score,
        decimal? coverage,
        int riskCount)
    {
        if (evaluation is null)
        {
            return "processing";
        }

        if (evaluation.Status is "failed")
        {
            return "error";
        }

        if (evaluation.Status is not "completed")
        {
            return "processing";
        }

        if (riskCount > 0 || coverage is < 0.5m)
        {
            return "request_info";
        }

        if (score is >= 75 && coverage is >= 0.6m)
        {
            return "shortlist";
        }

        return "review";
    }
}
