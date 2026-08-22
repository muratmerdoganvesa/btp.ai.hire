using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using HireLens.Contracts;
using HireLens.Infrastructure.Btp;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;

namespace HireLens.Api.Auth;

public static class JwtDebug
{
    public static string? LastFailure { get; set; }
}

public static class AuthRegistration
{
    public static IServiceCollection AddHireLensAuth(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();
        JwtSecurityTokenHandler.DefaultOutboundClaimTypeMap.Clear();
        services.AddSingleton<IClaimsTransformation, RoleClaimsTransformation>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options => ConfigureJwt(options, configuration, environment));

        services.AddAuthorization(options =>
        {
            options.AddPolicy(Roles.Recruiter, policy => policy.RequireRole(Roles.Recruiter));
            options.AddPolicy(Roles.HiringManager, policy => policy.RequireRole(Roles.HiringManager));
            options.AddPolicy(Roles.TenantAdmin, policy => policy.RequireRole(Roles.TenantAdmin));
            options.FallbackPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build();
        });

        return services;
    }

    private static void ConfigureJwt(
        JwtBearerOptions options,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        options.MapInboundClaims = false;

        if (environment.IsDevelopment() || environment.IsEnvironment("Testing"))
        {
            var key = Encoding.UTF8.GetBytes(DevAuth.SigningKey(configuration));
            options.RequireHttpsMetadata = false;
            options.IncludeErrorDetails = true;
            options.UseSecurityTokenValidators = true;
            options.TokenHandlers.Clear();
            options.TokenHandlers.Add(new JwtSecurityTokenHandler());
            options.Events = new JwtBearerEvents
            {
                OnAuthenticationFailed = context =>
                {
                    JwtDebug.LastFailure = $"{context.Exception.GetType().Name}: {context.Exception.Message}";
                    return Task.CompletedTask;
                }
            };
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = DevAuth.Issuer,
                ValidIssuers = [DevAuth.Issuer, DevAuth.IasIssuer],
                ValidateAudience = true,
                ValidAudience = DevAuth.Audience,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateLifetime = true,
                NameClaimType = "sub",
                RoleClaimType = ClaimTypes.Role,
                ClockSkew = TimeSpan.FromMinutes(1)
            };
            return;
        }

        var xsuaa = VcapServices.Find(configuration["VCAP_SERVICES"], "xsuaa");
        var authority = xsuaa?.Credentials.Url
            ?? configuration["XSUAA_URL"]
            ?? throw new InvalidOperationException("XSUAA binding or XSUAA_URL is required in this environment.");
        var audience = xsuaa?.Credentials.Extra.GetValueOrDefault("xsappname")
            ?? configuration["XSUAA_XSAPPNAME"]
            ?? "hirelens";

        options.Authority = authority;
        options.Audience = audience;
        options.RequireHttpsMetadata = true;
        options.TokenValidationParameters.NameClaimType = "sub";
        options.TokenValidationParameters.RoleClaimType = ClaimTypes.Role;
    }
}

public sealed class RoleClaimsTransformation : IClaimsTransformation
{
    public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity is not ClaimsIdentity identity)
        {
            return Task.FromResult(principal);
        }

        var scopes = principal.FindAll("scope")
            .Concat(principal.FindAll("xs.rolecollections"))
            .Select(c => c.Value);

        foreach (var scope in scopes)
        {
            foreach (var role in Roles.All)
            {
                if (scope.EndsWith(role, StringComparison.Ordinal) &&
                    !identity.HasClaim(ClaimTypes.Role, role))
                {
                    identity.AddClaim(new Claim(ClaimTypes.Role, role));
                }
            }
        }

        return Task.FromResult(principal);
    }
}
