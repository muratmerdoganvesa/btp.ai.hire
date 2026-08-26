using System.Net.Http.Headers;
using HireLens.Bff;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Serilog;
using Yarp.ReverseProxy.Transforms;

var builder = WebApplication.CreateBuilder(args);

var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
Console.WriteLine($"HireLens BFF binding to http://0.0.0.0:{port}");

builder.Host.UseSerilog((_, config) => config.WriteTo.Console());

var canonical = builder.Configuration["PUBLIC_HOST"] ?? CanonicalHost.Default;
var publicOrigin = CanonicalHost.Origin(canonical);
var apiUrl = (builder.Configuration["API_URL"] ?? "https://hirelens-api.cfapps.eu20-002.hana.ondemand.com").TrimEnd('/');
var xsuaa = XsuaaBinding.Read(builder.Configuration);
Console.WriteLine($"HireLens BFF xsuaa authority={xsuaa.Authority} clientId={xsuaa.ClientId[..Math.Min(12, xsuaa.ClientId.Length)]}…");

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
        options.DefaultSignOutScheme = OpenIdConnectDefaults.AuthenticationScheme;
    })
    .AddCookie(options =>
    {
        options.Cookie.Name = ".HireLens";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
    })
    .AddOpenIdConnect(options =>
    {
        options.Authority = xsuaa.Authority;
        options.MetadataAddress = xsuaa.Authority + "/.well-known/openid-configuration";
        options.ClientId = xsuaa.ClientId;
        options.ClientSecret = xsuaa.ClientSecret;
        options.ResponseType = OpenIdConnectResponseType.Code;
        options.CallbackPath = "/signin-oidc";
        options.SignedOutCallbackPath = "/signout-callback-oidc";
        options.SaveTokens = true;
        options.GetClaimsFromUserInfoEndpoint = false;
        options.UsePkce = true;
        options.RequireHttpsMetadata = true;
        options.MapInboundClaims = false;
        options.TokenValidationParameters.NameClaimType = "sub";
        options.TokenValidationParameters.ValidateAudience = false;
        options.Scope.Clear();
        options.Scope.Add("openid");
        options.NonceCookie.SameSite = SameSiteMode.Lax;
        options.NonceCookie.SecurePolicy = CookieSecurePolicy.Always;
        options.CorrelationCookie.SameSite = SameSiteMode.Lax;
        options.CorrelationCookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Events = new OpenIdConnectEvents
        {
            OnRedirectToIdentityProvider = context =>
            {
                context.ProtocolMessage.RedirectUri = $"{publicOrigin}/signin-oidc";
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddReverseProxy()
    .LoadFromMemory(
        [
            // Candidate-facing API — no recruiter cookie required.
            new Yarp.ReverseProxy.Configuration.RouteConfig
            {
                RouteId = "api-public-interviews",
                ClusterId = "api",
                AuthorizationPolicy = "Anonymous",
                Match = new Yarp.ReverseProxy.Configuration.RouteMatch
                {
                    Path = "/api/interviews/public/{**catch-all}"
                }
            },
            new Yarp.ReverseProxy.Configuration.RouteConfig
            {
                RouteId = "api-public",
                ClusterId = "api",
                AuthorizationPolicy = "Anonymous",
                Match = new Yarp.ReverseProxy.Configuration.RouteMatch
                {
                    Path = "/api/public/{**catch-all}"
                }
            },
            new Yarp.ReverseProxy.Configuration.RouteConfig
            {
                RouteId = "api",
                ClusterId = "api",
                Match = new Yarp.ReverseProxy.Configuration.RouteMatch { Path = "/api/{**catch-all}" }
            },
            new Yarp.ReverseProxy.Configuration.RouteConfig
            {
                RouteId = "compliance",
                ClusterId = "api",
                Match = new Yarp.ReverseProxy.Configuration.RouteMatch { Path = "/compliance/{**catch-all}" }
            }
        ],
        [
            new Yarp.ReverseProxy.Configuration.ClusterConfig
            {
                ClusterId = "api",
                Destinations = new Dictionary<string, Yarp.ReverseProxy.Configuration.DestinationConfig>
                {
                    ["api"] = new() { Address = apiUrl }
                }
            }
        ])
    .AddTransforms(builderContext =>
    {
        builderContext.AddRequestTransform(async transform =>
        {
            var token = await transform.HttpContext.GetTokenAsync("access_token");
            if (!string.IsNullOrWhiteSpace(token))
            {
                transform.ProxyRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }
        });
    });

var app = builder.Build();

app.UseForwardedHeaders();
app.Use(async (context, next) =>
{
    if (CanonicalHost.IsAlias(context.Request.Host.Host, canonical))
    {
        context.Response.Redirect($"{publicOrigin}{context.Request.PathBase}{context.Request.Path}{context.Request.QueryString}");
        return;
    }

    await next();
});
app.UseAuthentication();
app.UseAuthorization();
app.UseStaticFiles();

app.MapGet("/health/live", () => Results.Ok(new
{
    status = "Healthy",
    role = "bff",
    gitSha = Environment.GetEnvironmentVariable("GIT_SHA"),
    origin = publicOrigin
})).AllowAnonymous();
app.MapGet("/login/callback", () => Results.Content(
    "<html><body><p>Eski Approuter callback. Lutfen <a href=\"/\">ana sayfadan</a> tekrar giris yapin.</p></body></html>",
    "text/html; charset=utf-8")).AllowAnonymous();
app.MapGet("/logout", async (HttpContext context) =>
{
    await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    await context.SignOutAsync(OpenIdConnectDefaults.AuthenticationScheme);
    return Results.Redirect("/");
}).AllowAnonymous();
app.MapReverseProxy().RequireAuthorization();

// Interview tokens use dots (tenant.session.hmac). Default MapFallbackToFile uses
// the :nonfile constraint and returns 404 for those URLs — bind explicit public SPA routes.
app.MapFallbackToFile("/interview/{**path}", "index.html").AllowAnonymous();
app.MapFallbackToFile("/apply/{**path}", "index.html").AllowAnonymous();
app.MapFallbackToFile("index.html").RequireAuthorization();

Log.Information("HireLens BFF origin={Origin} api={Api} authority={Authority}", publicOrigin, apiUrl, xsuaa.Authority);
app.Run();

public partial class Program;
