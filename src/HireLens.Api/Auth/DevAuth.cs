using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using HireLens.Contracts;
using HireLens.Infrastructure.Btp;
using HireLens.Infrastructure.Tenancy;
using HireLens.Modules.Tenancy.Application;
using HireLens.SharedKernel;

namespace HireLens.Api.Auth;

public static class DevAuth
{
    public const string Issuer = "https://hirelens.local/dev";
    public const string IasIssuer = "https://accounts.ondemand.com/dev";
    public const string Audience = "hirelens-api";

    public static string SigningKey(IConfiguration configuration) =>
        configuration["DEV_JWT_SIGNING_KEY"] ?? "HireLens-dev-only-signing-key-32b!";

    public static bool IsEnabled(IHostEnvironment environment, IConfiguration configuration) =>
        HireLensAuthMode.UseDevTokens(environment, configuration);
}

public sealed record DevTokenRequest(
    Guid TenantId,
    string Subject,
    IReadOnlyList<string> Roles,
    string IssuerKind = "xsuaa");

public sealed record DevTokenResponse(
    [property: System.Text.Json.Serialization.JsonPropertyName("accessToken")] string AccessToken,
    [property: System.Text.Json.Serialization.JsonPropertyName("expiresAt")] DateTimeOffset ExpiresAt);

public static class DevTokenEndpoints
{
    public static void MapDevToken(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/dev/token", Issue)
            .AllowAnonymous()
            .WithTags("Development")
            .ExcludeFromDescription();
    }

    private static async Task<IResult> Issue(
        DevTokenRequest request,
        IConfiguration configuration,
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        if (request.TenantId == Guid.Empty || string.IsNullOrWhiteSpace(request.Subject))
        {
            return Results.BadRequest(new { error = "TenantId and Subject are required." });
        }

        var roles = request.Roles.Count == 0 ? new[] { Roles.Recruiter } : request.Roles;
        if (roles.Any(r => !Roles.All.Contains(r)))
        {
            return Results.BadRequest(new { error = "One or more roles are unknown." });
        }

        await EnsureTenantExists(services, request, cancellationToken);

        var issuer = request.IssuerKind.Equals("ias", StringComparison.OrdinalIgnoreCase)
            ? DevAuth.IasIssuer
            : DevAuth.Issuer;

        var tenantClaim = issuer == DevAuth.IasIssuer
            ? TenantClaimNames.IasTenant
            : TenantClaimNames.XsuaaTenant;

        var claims = new List<Claim>
        {
            new("sub", request.Subject),
            new(tenantClaim, request.TenantId.ToString())
        };

        foreach (var role in roles)
        {
            claims.Add(new Claim("role", role));
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(DevAuth.SigningKey(configuration)));
        var expires = DateTime.UtcNow.AddHours(8);
        var token = new JwtSecurityTokenHandler().CreateEncodedJwt(new SecurityTokenDescriptor
        {
            Issuer = issuer,
            Audience = DevAuth.Audience,
            Subject = new ClaimsIdentity(claims, "dev"),
            NotBefore = DateTime.UtcNow.AddMinutes(-1),
            Expires = expires,
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256)
        });

        return Results.Ok(new DevTokenResponse(token, expires));
    }

    private static async Task EnsureTenantExists(
        IServiceProvider services,
        DevTokenRequest request,
        CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();
        var tenantContext = scope.ServiceProvider.GetRequiredService<TenantContext>();
        tenantContext.Resolve(request.TenantId, request.Subject, "dev-token");
        var tenants = scope.ServiceProvider.GetRequiredService<ITenantService>();
        var current = await tenants.GetCurrentAsync(cancellationToken);
        if (current.IsSuccess)
        {
            return;
        }

        var slug = $"dev-{request.TenantId:N}"[..16];
        var provisioned = await tenants.ProvisionAsync(request.TenantId, "Development tenant", slug, cancellationToken);
        if (provisioned.IsFailure && provisioned.Error.Code != "conflict")
        {
            throw new InvalidOperationException(provisioned.Error.Message);
        }
    }
}
