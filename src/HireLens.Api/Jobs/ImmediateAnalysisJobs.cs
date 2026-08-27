using HireLens.Contracts.Matching;
using HireLens.Modules.Documents.Application;
using HireLens.Modules.Matching.Application;
using HireLens.SharedKernel;

namespace HireLens.Api.Jobs;

/// <summary>
/// Testing: run inline so integration tests do not race the worker.
/// Development/Production: enqueue and return — HTTP must not wait on AI Core.
/// </summary>
public sealed class ImmediateAnalysisJobs(
    IServiceScopeFactory scopes,
    AnalysisJobQueue queue,
    IHostEnvironment env) : IAnalysisJobs
{
    private readonly bool _runInline = env.IsEnvironment("Testing");

    public string EnqueueDocumentParse(Guid tenantId, Guid documentId, Guid jobId)
    {
        Dispatch(
            tenantId,
            AnalysisWorkKind.Parse,
            documentId,
            jobId,
            (sp, ct) => sp.GetRequiredService<ParseCvJob>().RunAsync(documentId, jobId, ct));
        return jobId.ToString("N");
    }

    public string EnqueueMatching(Guid tenantId, Guid documentId)
    {
        Dispatch(
            tenantId,
            AnalysisWorkKind.Match,
            documentId,
            documentId,
            (sp, ct) => sp.GetRequiredService<MatchingJob>().RunAsync(documentId, ct));
        return documentId.ToString("N");
    }

    public string EnqueueEvaluation(Guid tenantId, Guid evaluationId)
    {
        Dispatch(
            tenantId,
            AnalysisWorkKind.Evaluation,
            Guid.Empty,
            evaluationId,
            (sp, ct) => sp.GetRequiredService<MatchingJob>().RunEvaluationAsync(evaluationId, ct));
        return evaluationId.ToString("N");
    }

    private void Dispatch(
        Guid tenantId,
        AnalysisWorkKind kind,
        Guid documentId,
        Guid jobId,
        Func<IServiceProvider, CancellationToken, Task> work)
    {
        if (_runInline)
        {
            using var scope = scopes.CreateScope();
            var tenant = scope.ServiceProvider.GetRequiredService<TenantContext>();
            tenant.Resolve(tenantId, "system", "analysis-job");
            work(scope.ServiceProvider, CancellationToken.None).GetAwaiter().GetResult();
            return;
        }

        queue.Enqueue(new AnalysisWork(tenantId, kind, documentId, jobId));
    }
}
