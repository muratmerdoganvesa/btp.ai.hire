using HireLens.SharedKernel;

namespace HireLens.Modules.Analytics.Domain;

public sealed class PromptExperiment : ITenantEntity
{
    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public string TaskType { get; private set; } = string.Empty;

    public string VersionA { get; private set; } = "v1";

    public string VersionB { get; private set; } = "v2";

    public int SplitPercent { get; private set; } = 50;

    public static PromptExperiment Open(Guid tenantId, string taskType, string versionA, string versionB, int split) =>
        new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            TaskType = taskType,
            VersionA = versionA,
            VersionB = versionB,
            SplitPercent = Math.Clamp(split, 0, 100)
        };

    public string Assign(string subjectKey)
    {
        var bucket = Math.Abs(subjectKey.GetHashCode(StringComparison.Ordinal)) % 100;
        return bucket < SplitPercent ? VersionA : VersionB;
    }
}

public sealed class BenchmarkRun : ITenantEntity
{
    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public int Samples { get; private set; }

    public double Accuracy { get; private set; }

    public double ConsistencySpread { get; private set; }

    public double LatencyMs { get; private set; }

    public decimal Cost { get; private set; }

    public DateTimeOffset RanAt { get; private set; }

    public static BenchmarkRun Record(
        Guid tenantId,
        int samples,
        double accuracy,
        double spread,
        double latency,
        decimal cost,
        DateTimeOffset now) =>
        new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Samples = samples,
            Accuracy = accuracy,
            ConsistencySpread = spread,
            LatencyMs = latency,
            Cost = cost,
            RanAt = now
        };
}

public sealed class ParseCache : ITenantEntity
{
    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public string ContentHash { get; private set; } = string.Empty;

    public string MaskedText { get; private set; } = string.Empty;

    public DateTimeOffset CachedAt { get; private set; }

    public static ParseCache Store(Guid tenantId, string hash, string maskedText, DateTimeOffset now) =>
        new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ContentHash = hash,
            MaskedText = maskedText,
            CachedAt = now
        };
}
