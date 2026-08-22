using HireLens.Contracts;
using HireLens.Contracts.Tenancy;
using HireLens.Modules.Tenancy.Application;
using HireLens.SharedKernel;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace HireLens.Modules.Tenancy.Endpoints;

public static class TenancyEndpoints
{
    public static IEndpointRouteBuilder MapTenancyEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/tenants").WithTags("Tenancy").RequireAuthorization();

        group.MapGet("/current", GetCurrent).RequireAuthorization();
        group.MapGet("/{id:guid}", GetById).RequireAuthorization();
        group.MapPut("/current", UpdateCurrent).RequireAuthorization(Roles.TenantAdmin);

        return endpoints;
    }

    private static async Task<IResult> GetCurrent(ITenantService tenants, CancellationToken cancellationToken)
    {
        var result = await tenants.GetCurrentAsync(cancellationToken);
        return ToHttp(result);
    }

    private static async Task<IResult> GetById(Guid id, ITenantService tenants, CancellationToken cancellationToken)
    {
        var result = await tenants.GetByIdAsync(id, cancellationToken);
        return ToHttp(result);
    }

    private static async Task<IResult> UpdateCurrent(
        UpdateTenantRequest request,
        ITenantService tenants,
        CancellationToken cancellationToken)
    {
        var result = await tenants.UpdateCurrentAsync(request, cancellationToken);
        return ToHttp(result);
    }

    private static IResult ToHttp<T>(Result<T> result)
    {
        if (result.IsSuccess)
        {
            return Results.Ok(result.Value);
        }

        return result.Error.Code switch
        {
            "not_found" => Results.NotFound(),
            "validation" => Results.BadRequest(new { error = result.Error.Message }),
            "conflict" => Results.Conflict(new { error = result.Error.Message }),
            _ => Results.Problem(result.Error.Message)
        };
    }
}
