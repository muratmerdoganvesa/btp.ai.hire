using HireLens.Contracts;
using HireLens.Contracts.Identity;
using HireLens.Modules.Identity.Application;
using HireLens.SharedKernel;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace HireLens.Modules.Identity.Endpoints;

public static class IdentityEndpoints
{
    public static IEndpointRouteBuilder MapIdentityEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/identity/users").WithTags("Identity").RequireAuthorization();

        group.MapGet("/", List).RequireAuthorization();
        group.MapGet("/{id:guid}", GetById).RequireAuthorization();
        group.MapPost("/", Create).RequireAuthorization(Roles.TenantAdmin);
        group.MapPut("/{id:guid}", Update).RequireAuthorization(Roles.TenantAdmin);
        group.MapDelete("/{id:guid}", Delete).RequireAuthorization(Roles.TenantAdmin);

        return endpoints;
    }

    private static async Task<IResult> List(IUserService users, CancellationToken cancellationToken)
    {
        var result = await users.ListAsync(cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : Results.Problem(result.Error.Message);
    }

    private static async Task<IResult> GetById(Guid id, IUserService users, CancellationToken cancellationToken)
    {
        var result = await users.GetByIdAsync(id, cancellationToken);
        return ToHttp(result);
    }

    private static async Task<IResult> Create(
        CreateUserRequest request,
        IUserService users,
        CancellationToken cancellationToken)
    {
        var result = await users.CreateAsync(request, cancellationToken);
        return result.IsSuccess
            ? Results.Created($"/api/identity/users/{result.Value.Id}", result.Value)
            : ToHttp(result);
    }

    private static async Task<IResult> Update(
        Guid id,
        UpdateUserRequest request,
        IUserService users,
        CancellationToken cancellationToken)
    {
        var result = await users.UpdateAsync(id, request, cancellationToken);
        return ToHttp(result);
    }

    private static async Task<IResult> Delete(Guid id, IUserService users, CancellationToken cancellationToken)
    {
        var result = await users.DeleteAsync(id, cancellationToken);
        if (result.IsSuccess)
        {
            return Results.NoContent();
        }

        return result.Error.Code == "not_found"
            ? Results.NotFound()
            : Results.Problem(result.Error.Message);
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
