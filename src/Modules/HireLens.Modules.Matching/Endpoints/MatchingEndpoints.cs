using HireLens.Contracts.Matching;
using HireLens.Modules.Matching.Application;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace HireLens.Modules.Matching.Endpoints;

public static class MatchingEndpoints
{
    public static IEndpointRouteBuilder MapMatchingEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/candidates/{candidateId:guid}/evaluation", async (
            Guid candidateId,
            IEvaluationService evaluations,
            CancellationToken ct) =>
        {
            var evaluation = await evaluations.GetForCandidateAsync(candidateId, ct);
            return evaluation is null ? Results.NotFound() : Results.Ok(evaluation);
        }).WithTags("Matching").RequireAuthorization();

        endpoints.MapPost("/api/evaluations", async (
            StartEvaluationRequest body,
            IEvaluationService evaluations,
            CancellationToken ct) =>
        {
            try
            {
                var id = await evaluations.StartAsync(body.CandidateId, body.JobDescriptionId, ct);
                return Results.Accepted($"/api/evaluations/{id}", new { evaluationId = id });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }).WithTags("Matching").RequireAuthorization();

        endpoints.MapGet("/api/evaluations/{id:guid}", async (
            Guid id,
            IEvaluationService evaluations,
            CancellationToken ct) =>
        {
            var evaluation = await evaluations.GetByIdAsync(id, ct);
            return evaluation is null ? Results.NotFound() : Results.Ok(evaluation);
        }).WithTags("Matching").RequireAuthorization();

        endpoints.MapGet("/api/evaluations/{id:guid}/audit", async (
            Guid id,
            IEvaluationService evaluations,
            CancellationToken ct) =>
        {
            var audit = await evaluations.GetAuditAsync(id, ct);
            return audit is null ? Results.NotFound() : Results.Ok(audit);
        }).WithTags("Matching").RequireAuthorization();

        return endpoints;
    }
}

public sealed record StartEvaluationRequest(Guid CandidateId, Guid JobDescriptionId);
