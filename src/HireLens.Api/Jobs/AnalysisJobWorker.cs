using HireLens.Modules.Documents.Application;
using HireLens.Modules.Matching.Application;
using HireLens.SharedKernel;

namespace HireLens.Api.Jobs;

/// <summary>
/// Drains <see cref="AnalysisJobQueue"/> one job at a time so AI Core is not
/// stampeded and HTTP threads stay free.
/// </summary>
public sealed class AnalysisJobWorker(
    AnalysisJobQueue queue,
    IServiceScopeFactory scopes,
    IHostEnvironment env,
    ILogger<AnalysisJobWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (env.IsEnvironment("Testing"))
        {
            return;
        }

        await foreach (var work in queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                using var scope = scopes.CreateScope();
                var tenant = scope.ServiceProvider.GetRequiredService<TenantContext>();
                tenant.Resolve(work.TenantId, "system", "analysis-job");
                var sp = scope.ServiceProvider;
                switch (work.Kind)
                {
                    case AnalysisWorkKind.Parse:
                        await sp.GetRequiredService<ParseCvJob>()
                            .RunAsync(work.DocumentId, work.JobId, CancellationToken.None);
                        break;
                    case AnalysisWorkKind.Match:
                        await sp.GetRequiredService<MatchingJob>()
                            .RunAsync(work.DocumentId, CancellationToken.None);
                        break;
                    case AnalysisWorkKind.Evaluation:
                        await sp.GetRequiredService<MatchingJob>()
                            .RunEvaluationAsync(work.JobId, CancellationToken.None);
                        break;
                }
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Analysis job {Kind} failed document={DocumentId} job={JobId}",
                    work.Kind,
                    work.DocumentId,
                    work.JobId);
            }
        }
    }
}
