using System.Security.Claims;
using HireLens.SharedKernel;
using Microsoft.AspNetCore.Http;

namespace HireLens.Infrastructure.Tenancy;

public static class TenantClaimNames
{
    public const string XsuaaTenant = "zid";
    public const string IasTenant = "app_tid";
}

/// <summary>
/// Tenant identity comes only from the validated JWT. XSUAA uses `zid`;
/// IAS OIDC uses `app_tid`. Issuer decides which claim is authoritative.
/// Absence is 401 — never a fallback tenant.
/// </summary>
public sealed class TenantResolutionMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, ITenantContext tenantContext)
    {
        if (ShouldSkip(context.Request.Path))
        {
            await next(context);
            return;
        }

        if (context.User.Identity?.IsAuthenticated != true)
        {
            await next(context);
            return;
        }

        var tenantId = ReadTenantId(context.User);
        if (tenantId is null)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { error = "tenant_claim_missing" });
            return;
        }

        var subject = context.User.FindFirstValue("sub") ?? context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        var correlation = context.TraceIdentifier;

        if (tenantContext is TenantContext mutable)
        {
            mutable.Resolve(tenantId.Value, subject, correlation);
        }

        await next(context);
    }

    internal static Guid? ReadTenantId(ClaimsPrincipal user)
    {
        var issuer = user.FindFirstValue("iss") ?? string.Empty;
        var preferred = IsIasIssuer(issuer) ? TenantClaimNames.IasTenant : TenantClaimNames.XsuaaTenant;
        var raw = user.FindFirstValue(preferred)
            ?? user.FindFirstValue(TenantClaimNames.XsuaaTenant)
            ?? user.FindFirstValue(TenantClaimNames.IasTenant);

        return Guid.TryParse(raw, out var tenantId) ? tenantId : null;
    }

    private static bool IsIasIssuer(string issuer) =>
        issuer.Contains("accounts.ondemand.com", StringComparison.OrdinalIgnoreCase) ||
        issuer.Contains("/oauth2/token", StringComparison.OrdinalIgnoreCase) is false &&
        issuer.Contains("ias", StringComparison.OrdinalIgnoreCase);

    private static bool ShouldSkip(PathString path) =>
        path.StartsWithSegments("/health") ||
        path.StartsWithSegments("/openapi") ||
        path.StartsWithSegments("/dev");
}
