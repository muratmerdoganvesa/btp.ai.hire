using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using HireLens.Contracts;
using HireLens.Infrastructure.Btp;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.IdentityModel.Tokens;
using Serilog;

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
        options.IncludeErrorDetails = true;
        options.UseSecurityTokenValidators = true;
        options.TokenHandlers.Clear();
        options.TokenHandlers.Add(new JwtSecurityTokenHandler { MapInboundClaims = false });
        options.Events = CreateEvents();
        Environment.SetEnvironmentVariable("HIRELENS_AUTH_MODE", HireLensAuthMode.Name(environment, configuration));

        if (HireLensAuthMode.HasXsuaa(configuration))
        {
            ConfigureXsuaaJwt(options, configuration);
            return;
        }

        if (DevAuth.IsEnabled(environment, configuration))
        {
            var key = Encoding.UTF8.GetBytes(DevAuth.SigningKey(configuration));
            options.RequireHttpsMetadata = false;
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

        throw new InvalidOperationException("XSUAA binding or XSUAA_URL is required in this environment.");
    }

    private static void ConfigureXsuaaJwt(JwtBearerOptions options, IConfiguration configuration)
    {
        var xsuaa = VcapServices.Find(HireLensAuthMode.ReadVcap(configuration), "xsuaa");
        var uaaUrl = xsuaa?.Credentials.Extra.GetValueOrDefault("uaa.url");
        var authority = (string.IsNullOrWhiteSpace(uaaUrl) ? xsuaa?.Credentials.Url : uaaUrl)
            ?? configuration["XSUAA_URL"]
            ?? throw new InvalidOperationException("XSUAA binding or XSUAA_URL is required in this environment.");
        authority = authority.TrimEnd('/');
        var verificationKey = xsuaa?.Credentials.Extra.GetValueOrDefault("verificationkey")
            ?? xsuaa?.Credentials.Extra.GetValueOrDefault("verificationKey")
            ?? xsuaa?.Credentials.Extra.GetValueOrDefault("uaa.verificationkey");

        options.RequireHttpsMetadata = true;
        options.TokenValidationParameters = XsuaaJwt.CreateParameters(authority, verificationKey);
        Log.Information(
            "XSUAA JWT authority={Authority} verificationKey={HasKey}",
            authority,
            !string.IsNullOrWhiteSpace(verificationKey));
    }

    private static JwtBearerEvents CreateEvents() => new()
    {
        OnMessageReceived = context =>
        {
            if (!string.IsNullOrEmpty(context.Token))
            {
                return Task.CompletedTask;
            }

            var header = context.Request.Headers.Authorization.ToString();
            if (header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                context.Token = header["Bearer ".Length..].Trim();
                return Task.CompletedTask;
            }

            var forwarded = context.Request.Headers["x-approuter-authorization"].FirstOrDefault()
                ?? context.Request.Headers["x-forwarded-access-token"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(forwarded))
            {
                return Task.CompletedTask;
            }

            context.Token = forwarded.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                ? forwarded["Bearer ".Length..].Trim()
                : forwarded.Trim();
            return Task.CompletedTask;
        },
        OnAuthenticationFailed = context =>
        {
            JwtDebug.LastFailure = $"{context.Exception.GetType().Name}: {context.Exception.Message}";
            Log.Warning("JWT rejected: {Message}", context.Exception.Message);
            return Task.CompletedTask;
        },
        OnChallenge = async context =>
        {
            context.HandleResponse();
            if (context.Response.HasStarted)
            {
                return;
            }

            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new
            {
                error = "jwt_rejected",
                detail = JwtDebug.LastFailure ?? "missing_or_invalid_bearer",
                hasAuthorization = context.HttpContext.Request.Headers.ContainsKey("Authorization")
            });
        }
    };
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
