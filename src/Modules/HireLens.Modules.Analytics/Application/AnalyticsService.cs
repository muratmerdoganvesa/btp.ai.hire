using System.Reflection;
using HireLens.Contracts.Analytics;
using HireLens.Contracts.Documents;
using HireLens.Infrastructure.Persistence;
using HireLens.Modules.Analytics.Domain;
using HireLens.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace HireLens.Modules.Analytics.Application;

public interface IAnalyticsService
{
    Task<Result<FunnelDto>> FunnelAsync(CancellationToken cancellationToken);

    Task<Result<IReadOnlyList<RecruiterLoadDto>>> LoadAsync(CancellationToken cancellationToken);

    Task<Result<IReadOnlyList<SourcePerfDto>>> SourcesAsync(CancellationToken cancellationToken);

    Task<Result<IReadOnlyList<BiasBucketDto>>> BiasAsync(CancellationToken cancellationToken);

    Task<Result<DriftDto>> DriftAsync(CancellationToken cancellationToken);

    Task<Result<CostReportDto>> CostAsync(CancellationToken cancellationToken);

    Task<Result<BenchmarkResultDto>> BenchmarkAsync(CancellationToken cancellationToken);

    Task<Result<PromptExperimentDto>> OpenExperimentAsync(PromptExperimentDto request, CancellationToken cancellationToken);
}

public sealed class AnalyticsService(
    HireLensDbContext db,
    ITenantContext tenant,
    IClock clock) : IAnalyticsService, IPromptExperimentPort, IParseCache
{
    public async Task<Result<FunnelDto>> FunnelAsync(CancellationToken cancellationToken)
    {
        RepositoryGuard.RequireTenant(tenant);
        return Result.Success(new FunnelDto(
            await CountAsync("HireLens.Modules.Recruiting.Domain.Position", cancellationToken),
            await CountAsync("HireLens.Modules.Candidate.Domain.Candidate", cancellationToken),
            await CountAsync("HireLens.Modules.Matching.Domain.Evaluation", cancellationToken),
            await CountAsync("HireLens.Modules.Interview.Domain.InterviewSession", cancellationToken),
            await CountAsync("HireLens.Modules.Review.Domain.Decision", cancellationToken)));
    }

    public async Task<Result<IReadOnlyList<RecruiterLoadDto>>> LoadAsync(CancellationToken cancellationToken)
    {
        RepositoryGuard.RequireTenant(tenant);
        var events = await db.AuditEvents
            .Where(e => e.EntityType.Contains("Decision"))
            .ToListAsync(cancellationToken);
        var grouped = events
            .GroupBy(e => e.ActorSubject ?? "unknown")
            .Select(g => new RecruiterLoadDto(g.Key, g.Count()))
            .ToList();
        return Result.Success<IReadOnlyList<RecruiterLoadDto>>(grouped);
    }

    public async Task<Result<IReadOnlyList<SourcePerfDto>>> SourcesAsync(CancellationToken cancellationToken)
    {
        RepositoryGuard.RequireTenant(tenant);
        var scores = await IntPropertyAsync("HireLens.Modules.Matching.Domain.Evaluation", "OverallScore", cancellationToken);
        return Result.Success<IReadOnlyList<SourcePerfDto>>(
            [new SourcePerfDto("cv", scores.Count, scores.Count == 0 ? null : scores.Average())]);
    }

    public async Task<Result<IReadOnlyList<BiasBucketDto>>> BiasAsync(CancellationToken cancellationToken)
    {
        RepositoryGuard.RequireTenant(tenant);
        var scores = await IntPropertyAsync("HireLens.Modules.Matching.Domain.Evaluation", "OverallScore", cancellationToken);
        string[] buckets = ["limited", "partial", "solid", "strong"];
        var rows = buckets.Select(band => new BiasBucketDto(band, scores.Count(s => Band(s) == band))).ToList();
        return Result.Success<IReadOnlyList<BiasBucketDto>>(rows);
    }

    public async Task<Result<DriftDto>> DriftAsync(CancellationToken cancellationToken)
    {
        RepositoryGuard.RequireTenant(tenant);
        var scores = await IntPropertyAsync("HireLens.Modules.Matching.Domain.Evaluation", "OverallScore", cancellationToken);
        var recent = scores.TakeLast(Math.Max(1, scores.Count / 2)).DefaultIfEmpty(0).Average();
        var previous = scores.Take(Math.Max(1, scores.Count / 2)).DefaultIfEmpty(0).Average();
        return Result.Success(new DriftDto(previous, recent, Math.Abs(recent - previous) >= 8));
    }

    public async Task<Result<CostReportDto>> CostAsync(CancellationToken cancellationToken)
    {
        RepositoryGuard.RequireTenant(tenant);
        var cached = await db.Set<ParseCache>().CountAsync(cancellationToken);
        var parses = await CountAsync("HireLens.Modules.Documents.Domain.CvDocument", cancellationToken);
        var invocations = await db.AiInvocations.ToListAsync(cancellationToken);
        var cheap = invocations.Count == 0
            ? 0
            : invocations.Count(i => i.ModelId.Contains("mini", StringComparison.OrdinalIgnoreCase)) / (double)invocations.Count;
        return Result.Success(new CostReportDto(cached, parses, cheap, invocations.Sum(i => i.EstimatedCost)));
    }

    public async Task<Result<BenchmarkResultDto>> BenchmarkAsync(CancellationToken cancellationToken)
    {
        RepositoryGuard.RequireTenant(tenant);
        var samples = new[] { ("Five years of C# and SQL.", 80), ("Pottery and gardening.", 0) };
        var started = clock.UtcNow;
        var predicted = samples.Select(s => s.Item1.Contains("C#", StringComparison.OrdinalIgnoreCase) ? 78 : 0).ToList();
        var accuracy = predicted.Zip(samples, (p, s) => Math.Abs(p - s.Item2) <= 10 ? 1.0 : 0.0).Average();
        var run = BenchmarkRun.Record(
            tenant.TenantId,
            samples.Length,
            accuracy,
            0,
            (clock.UtcNow - started).TotalMilliseconds,
            0,
            clock.UtcNow);
        db.Set<BenchmarkRun>().Add(run);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success(new BenchmarkResultDto(run.Samples, run.Accuracy, run.ConsistencySpread, run.LatencyMs, run.Cost));
    }

    public async Task<Result<PromptExperimentDto>> OpenExperimentAsync(PromptExperimentDto request, CancellationToken cancellationToken)
    {
        RepositoryGuard.RequireTenant(tenant);
        var existing = await db.Set<PromptExperiment>().SingleOrDefaultAsync(e => e.TaskType == request.TaskType, cancellationToken);
        if (existing is not null)
        {
            return Result.Success(new PromptExperimentDto(existing.TaskType, existing.VersionA, existing.VersionB, existing.SplitPercent));
        }

        var row = PromptExperiment.Open(tenant.TenantId, request.TaskType, request.VersionA, request.VersionB, request.SplitPercent);
        db.Set<PromptExperiment>().Add(row);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success(new PromptExperimentDto(row.TaskType, row.VersionA, row.VersionB, row.SplitPercent));
    }

    public async Task<string?> AssignVersionAsync(string taskType, string subjectKey, CancellationToken cancellationToken)
    {
        if (!tenant.IsResolved)
        {
            return null;
        }

        var experiment = await db.Set<PromptExperiment>().SingleOrDefaultAsync(e => e.TaskType == taskType, cancellationToken);
        return experiment?.Assign(subjectKey);
    }

    public async Task<string?> TryGetAsync(string contentHash, CancellationToken cancellationToken)
    {
        RepositoryGuard.RequireTenant(tenant);
        var row = await db.Set<ParseCache>().SingleOrDefaultAsync(c => c.ContentHash == contentHash, cancellationToken);
        return row?.MaskedText;
    }

    public async Task PutAsync(string contentHash, string maskedText, CancellationToken cancellationToken)
    {
        RepositoryGuard.RequireTenant(tenant);
        if (await db.Set<ParseCache>().AnyAsync(c => c.ContentHash == contentHash, cancellationToken))
        {
            return;
        }

        db.Set<ParseCache>().Add(ParseCache.Store(tenant.TenantId, contentHash, maskedText, clock.UtcNow));
        await db.SaveChangesAsync(cancellationToken);
    }

    public Task<int> CountAsync(CancellationToken cancellationToken)
    {
        RepositoryGuard.RequireTenant(tenant);
        return db.Set<ParseCache>().CountAsync(cancellationToken);
    }

    private async Task<int> CountAsync(string clrName, CancellationToken cancellationToken)
    {
        var type = ResolveType(clrName);
        if (type is null)
        {
            return 0;
        }

        var method = typeof(AnalyticsService).GetMethod(nameof(CountSet), BindingFlags.Instance | BindingFlags.NonPublic)!
            .MakeGenericMethod(type);
        return await (Task<int>)method.Invoke(this, [cancellationToken])!;
    }

    private Task<int> CountSet<T>(CancellationToken cancellationToken) where T : class =>
        db.Set<T>().CountAsync(cancellationToken);

    private async Task<List<int>> IntPropertyAsync(string clrName, string property, CancellationToken cancellationToken)
    {
        var type = ResolveType(clrName);
        if (type is null)
        {
            return [];
        }

        var method = typeof(AnalyticsService).GetMethod(nameof(ReadInt), BindingFlags.Instance | BindingFlags.NonPublic)!
            .MakeGenericMethod(type);
        return await (Task<List<int>>)method.Invoke(this, [property, cancellationToken])!;
    }

    private async Task<List<int>> ReadInt<T>(string property, CancellationToken cancellationToken) where T : class
    {
        var rows = await db.Set<T>().ToListAsync(cancellationToken);
        var info = typeof(T).GetProperty(property);
        return rows
            .Select(row => info?.GetValue(row) as int?)
            .Where(v => v is not null)
            .Select(v => v!.Value)
            .ToList();
    }

    private Type? ResolveType(string clrName) =>
        AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(assembly =>
            {
                try
                {
                    return assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException)
                {
                    return [];
                }
            })
            .FirstOrDefault(type => type.FullName == clrName);

    private static string Band(int score) =>
        score >= 85 ? "strong" : score >= 70 ? "solid" : score >= 55 ? "partial" : "limited";
}
