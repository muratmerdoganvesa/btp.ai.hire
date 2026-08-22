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
        group.MapGet("/", async (IPositionService svc, CancellationToken ct) => HttpResults.From(await svc.ListAsync(ct)));
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
        return endpoints;
    }
}
