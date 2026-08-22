using HireLens.Contracts.Compliance;
using HireLens.Infrastructure.Hosting;
using HireLens.Modules.Compliance.Application;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace HireLens.Modules.Compliance.Endpoints;

public static class ComplianceEndpoints
{
    public static IEndpointRouteBuilder MapComplianceEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/compliance/export/{candidateId:guid}", async (
            Guid candidateId,
            IComplianceService compliance,
            CancellationToken ct) => HttpResults.From(await compliance.ExportAsync(candidateId, ct)))
            .WithTags("Compliance")
            .RequireAuthorization();

        endpoints.MapPost("/compliance/data-deletion-requests", async (
            CreateDeletionRequest request,
            IComplianceService compliance,
            CancellationToken ct) =>
        {
            var result = await compliance.RequestDeletionAsync(request, ct);
            return result.IsSuccess ? Results.Created($"/compliance/data-deletion-requests/{result.Value.Id}", result.Value) : HttpResults.From(result);
        }).WithTags("Compliance").RequireAuthorization();

        endpoints.MapGet("/compliance/data-deletion-requests", async (
            IComplianceService compliance,
            CancellationToken ct) => HttpResults.From(await compliance.ListDeletionsAsync(ct)))
            .WithTags("Compliance")
            .RequireAuthorization();

        return endpoints;
    }
}
