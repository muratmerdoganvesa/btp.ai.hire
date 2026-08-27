using HireLens.Contracts.Recruiting;
using HireLens.Infrastructure.Hosting;
using HireLens.Modules.Recruiting.Application;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace HireLens.Modules.Recruiting.Endpoints;

public static class RecruitingEndpoints
{
    public static IEndpointRouteBuilder MapRecruitingEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/positions").WithTags("Recruiting").RequireAuthorization();
        group.MapGet("/", async (bool? includeStats, IPositionService svc, CancellationToken ct) =>
            HttpResults.From(await svc.ListAsync(includeStats ?? false, ct)));
        group.MapGet("/{id:guid}", async (Guid id, IPositionService svc, CancellationToken ct) => HttpResults.From(await svc.GetAsync(id, ct)));
        group.MapPost("/", async (UpsertPositionRequest request, IPositionService svc, CancellationToken ct) =>
        {
            var result = await svc.CreateAsync(request, ct);
            return result.IsSuccess
                ? Results.Created($"/api/positions/{result.Value.Id}", result.Value)
                : HttpResults.From(result);
        });
        group.MapPut("/{id:guid}", async (Guid id, UpsertPositionRequest request, IPositionService svc, CancellationToken ct) =>
            HttpResults.From(await svc.UpdateAsync(id, request, ct)));
        group.MapDelete("/{id:guid}", async (Guid id, IPositionService svc, CancellationToken ct) =>
            HttpResults.From(await svc.SoftDeleteAsync(id, ct)));

        var jobs = endpoints.MapGroup("/api/jobs").WithTags("Recruiting").RequireAuthorization();
        jobs.MapGet("/", async (bool? includeStats, IPositionService svc, CancellationToken ct) =>
            HttpResults.From(await svc.ListAsync(includeStats ?? false, ct)));
        jobs.MapPost("/criteria/extract", async (
            ExtractCriteriaRequest request,
            ICriteriaExtractionService svc,
            CancellationToken ct) =>
        {
            var result = await svc.ExtractAsync(request, ct);
            if (result.IsSuccess)
            {
                return Results.Ok(result.Value);
            }

            if (result.Error.Code == "validation")
            {
                return Results.BadRequest(new { error = result.Error.Message });
            }

            return Results.Json(new { error = result.Error.Message }, statusCode: StatusCodes.Status503ServiceUnavailable);
        });
        return endpoints;
    }
}
