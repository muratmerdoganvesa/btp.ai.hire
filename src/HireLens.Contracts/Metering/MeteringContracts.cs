namespace HireLens.Contracts.Metering;

public sealed record QuotaDto(int MonthlyTokenLimit, string OveragePolicy, int UsedTokens, int RemainingTokens);

public sealed record UsagePointDto(DateTimeOffset At, string Kind, int Amount, string? ModelId);

public interface IQuotaGuard
{
    Task<SharedKernel.Result> EnsureCanInvokeAsync(int estimatedTokens, CancellationToken cancellationToken);
}

public interface IQuotaBootstrap
{
    Task EnsureDefaultAsync(CancellationToken cancellationToken);
}
