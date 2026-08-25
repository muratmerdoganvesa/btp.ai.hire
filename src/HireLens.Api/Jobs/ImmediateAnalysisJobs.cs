using HireLens.Contracts.Matching;
using HireLens.Modules.Documents.Application;
using HireLens.Modules.Matching.Application;
using HireLens.SharedKernel;

namespace HireLens.Api.Jobs;

/// <summary>
/// Executes parse and match on the calling thread so Development and tests
/// do not depend on a separate Hangfire worker process.
/// </summary>
public sealed class ImmediateAnalysisJobs(IServiceScopeFactory scopes) : IAnalysisJobs
{
    public string EnqueueDocumentParse(Guid tenantId, Guid documentId)
    {
        Run(tenantId, (sp, ct) => sp.GetRequiredService<ParseCvJob>().RunAsync(documentId, ct));
        return documentId.ToString("N");
    }

    public string EnqueueMatching(Guid tenantId, Guid documentId)
    {
        Run(tenantId, (sp, ct) => sp.GetRequiredService<MatchingJob>().RunAsync(documentId, ct));
        return documentId.ToString("N");
    }

    public string EnqueueEvaluation(Guid tenantId, Guid evaluationId)
    {
        Run(tenantId, (sp, ct) => sp.GetRequiredService<MatchingJob>().RunEvaluationAsync(evaluationId, ct));
        return evaluationId.ToString("N");
    }

    private void Run(Guid tenantId, Func<IServiceProvider, CancellationToken, Task> work)
    {
        using var scope = scopes.CreateScope();
        var tenant = scope.ServiceProvider.GetRequiredService<TenantContext>();
        tenant.Resolve(tenantId, "system", "analysis-job");
        work(scope.ServiceProvider, CancellationToken.None).GetAwaiter().GetResult();
    }
}
