using HireLens.Contracts.Metering;
using HireLens.Infrastructure.Persistence;
using HireLens.Modules.Metering.Domain;
using HireLens.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace HireLens.Modules.Metering.Application;

public interface IMeteringService
{
    Task<Result<QuotaDto>> GetQuotaAsync(CancellationToken cancellationToken);

    Task<Result<IReadOnlyList<UsagePointDto>>> ListUsageAsync(CancellationToken cancellationToken);
}

public sealed class MeteringService(HireLensDbContext db, ITenantContext tenant, IClock clock) : IMeteringService, IQuotaGuard, IQuotaBootstrap
{
    public async Task<Result<QuotaDto>> GetQuotaAsync(CancellationToken cancellationToken)
    {
        RepositoryGuard.RequireTenant(tenant);
        var quota = await db.Set<TenantQuota>().SingleOrDefaultAsync(cancellationToken) ?? TenantQuota.Default(tenant.TenantId);
        var used = await UsedThisMonthAsync(cancellationToken);
        return Result.Success(new QuotaDto(quota.MonthlyTokenLimit, quota.OveragePolicy, used, Math.Max(0, quota.MonthlyTokenLimit - used)));
    }

    public async Task<Result<IReadOnlyList<UsagePointDto>>> ListUsageAsync(CancellationToken cancellationToken)
    {
        RepositoryGuard.RequireTenant(tenant);
        var rows = await db.Set<AiInvocation>()
            .OrderByDescending(i => i.OccurredAt)
            .Take(100)
            .ToListAsync(cancellationToken);
        return Result.Success<IReadOnlyList<UsagePointDto>>(
            rows.Select(i => new UsagePointDto(i.OccurredAt, i.TaskType, i.InputTokens + i.OutputTokens, i.ModelId)).ToList());
    }

    public async Task<Result> EnsureCanInvokeAsync(int estimatedTokens, CancellationToken cancellationToken)
    {
        RepositoryGuard.RequireTenant(tenant);
        var quota = await db.Set<TenantQuota>().SingleOrDefaultAsync(cancellationToken);
        if (quota is null || quota.OveragePolicy != "block")
        {
            return Result.Success();
        }

        var used = await UsedThisMonthAsync(cancellationToken);
        return used + estimatedTokens > quota.MonthlyTokenLimit
            ? Result.Failure(Error.Validation("Monthly token quota would be exceeded."))
            : Result.Success();
    }

    public async Task EnsureDefaultAsync(CancellationToken cancellationToken)
    {
        RepositoryGuard.RequireTenant(tenant);
        if (!await db.Set<TenantQuota>().AnyAsync(cancellationToken))
        {
            db.Set<TenantQuota>().Add(TenantQuota.Default(tenant.TenantId));
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task<int> UsedThisMonthAsync(CancellationToken cancellationToken)
    {
        var start = new DateTimeOffset(clock.UtcNow.Year, clock.UtcNow.Month, 1, 0, 0, 0, TimeSpan.Zero);
        return await db.Set<AiInvocation>()
            .Where(i => i.OccurredAt >= start)
            .SumAsync(i => i.InputTokens + i.OutputTokens, cancellationToken);
    }
}
