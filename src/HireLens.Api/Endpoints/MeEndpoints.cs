using System.Security.Claims;
using HireLens.SharedKernel;

namespace HireLens.Api.Endpoints;

public static class MeEndpoints
{
    public static IEndpointRouteBuilder MapMeEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/me", (ClaimsPrincipal user, ITenantContext tenant) =>
        {
            var roles = user.FindAll(ClaimTypes.Role).Select(c => c.Value).Distinct().ToArray();
            var subject = user.FindFirstValue("sub")
                ?? user.FindFirstValue("user_id")
                ?? user.FindFirstValue("user_name")
                ?? user.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? user.FindFirstValue("email")
                ?? user.Identity?.Name;
            return Results.Ok(new
            {
                subject,
                tenantId = tenant.IsResolved ? tenant.TenantId : (Guid?)null,
                roles
            });
        }).WithTags("Identity");

        return endpoints;
    }
}
