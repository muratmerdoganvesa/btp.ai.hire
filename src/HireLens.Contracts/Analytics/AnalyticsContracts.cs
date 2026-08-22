namespace HireLens.Contracts.Analytics;

public sealed record FunnelDto(int Positions, int Candidates, int Evaluations, int Interviews, int Decisions);

public sealed record RecruiterLoadDto(string Subject, int Decisions);

public sealed record SourcePerfDto(string Source, int Count, double? AverageScore);

public sealed record BiasBucketDto(string Band, int Count);

public sealed record DriftDto(double PreviousMean, double RecentMean, bool Alert);

public sealed record CostReportDto(int CachedParses, int TotalParses, double CheapModelRatio, decimal EstimatedCost);

public sealed record PromptExperimentDto(string TaskType, string VersionA, string VersionB, int SplitPercent);

public sealed record BenchmarkResultDto(
    int Samples,
    double Accuracy,
    double ConsistencySpread,
    double LatencyMs,
    decimal Cost);

public interface IPromptExperimentPort
{
    Task<string?> AssignVersionAsync(string taskType, string subjectKey, CancellationToken cancellationToken);
}
