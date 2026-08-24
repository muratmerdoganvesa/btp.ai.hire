using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
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
        var raw = FirstClaim(user, preferred, TenantClaimNames.XsuaaTenant, TenantClaimNames.IasTenant, "zone_uuid")
            ?? ZidFromExtAttr(user);

        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        raw = UnwrapClaimValue(raw);
        return Guid.TryParse(raw, out var tenantId) ? tenantId : GuidFromTenantKey(raw);
    }

    internal static string UnwrapClaimValue(string raw)
    {
        raw = raw.Trim();
        if (!raw.StartsWith('[') || !raw.EndsWith(']'))
        {
            return raw;
        }

        try
        {
            using var document = JsonDocument.Parse(raw);
            if (document.RootElement.ValueKind == JsonValueKind.Array && document.RootElement.GetArrayLength() > 0)
            {
                var first = document.RootElement[0];
                return first.ValueKind == JsonValueKind.String ? first.GetString() ?? raw : first.ToString();
            }
        }
        catch (JsonException)
        {
            return raw;
        }

        return raw;
    }

    private static string? FirstClaim(ClaimsPrincipal user, params string[] names)
    {
        foreach (var name in names)
        {
            var value = user.FindFirstValue(name);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    private static string? ZidFromExtAttr(ClaimsPrincipal user)
    {
        var extAttr = user.FindFirstValue("ext_attr");
        if (string.IsNullOrWhiteSpace(extAttr) || !extAttr.TrimStart().StartsWith('{'))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(extAttr);
            return document.RootElement.TryGetProperty("zid", out var zid)
                ? zid.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    internal static Guid GuidFromTenantKey(string key)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes("hirelens:" + key));
        return new Guid(hash.AsSpan(0, 16));
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
