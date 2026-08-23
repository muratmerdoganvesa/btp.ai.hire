using System.Text.Json.Serialization;
using HireLens.AiGateway;
using HireLens.Api.Auth;
using HireLens.Api.Endpoints;
using HireLens.Api.Hosting;
using HireLens.Api.Jobs;
using HireLens.Api.Seed;
using HireLens.Contracts.Matching;
using HireLens.Infrastructure.Btp;
using HireLens.Infrastructure.Hosting;
using HireLens.Infrastructure.Persistence;
using HireLens.Infrastructure.Storage;
using HireLens.Infrastructure.Tenancy;
using HireLens.Modules.Analytics;
using HireLens.Modules.Candidate;
using HireLens.Modules.Compliance;
using HireLens.Modules.Configuration;
using HireLens.Modules.Documents;
using HireLens.Modules.Evidence;
using HireLens.Modules.Identity;
using HireLens.Modules.Integration;
using HireLens.Modules.Interview;
using HireLens.Modules.Matching;
using HireLens.Modules.Metering;
using HireLens.Modules.Notification;
using HireLens.Modules.Privacy;
using HireLens.Modules.Recruiting;
using HireLens.Modules.Review;
using HireLens.Modules.Taxonomy;
using HireLens.Modules.Tenancy;
using Microsoft.EntityFrameworkCore;
using Serilog;

ModuleLoad.Ensure();

var builder = WebApplication.CreateBuilder(args);

var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(port))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}

builder.Host.UseHireLensSerilog();
builder.Services.AddHireLensTelemetry(builder.Configuration);
builder.Services.AddHireLensPersistence(builder.Configuration, builder.Environment);
builder.Services.AddTenancyModule();
builder.Services.AddIdentityModule();
builder.Services.AddRecruitingModule();
builder.Services.AddCandidateModule();
builder.Services.AddDocumentsModule();
builder.Services.AddEvidenceModule();
builder.Services.AddMatchingModule();
builder.Services.AddReviewModule();
builder.Services.AddComplianceModule();
builder.Services.AddTaxonomyModule();
builder.Services.AddPrivacyModule();
builder.Services.AddNotificationModule();
builder.Services.AddMeteringModule();
builder.Services.AddAnalyticsModule();
builder.Services.AddConfigurationModule();
builder.Services.AddIntegrationModule();
builder.Services.AddInterviewModule();
builder.Services.AddSingleton<IObjectStore, LocalObjectStore>();
builder.Services.AddSingleton<IFileGuard, FileGuard>();
builder.Services.AddSingleton<IAnalysisJobs, ImmediateAnalysisJobs>();
builder.Services.AddScoped<IDemoSeedService, DemoSeedService>();
builder.Services.AddAiGateway(builder.Configuration);
builder.Services.AddHireLensAuth(builder.Configuration, builder.Environment);
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks()
    .AddDbContextCheck<HireLensDbContext>("database", tags: ["ready"]);

var auditBinding = VcapServices.Find(builder.Configuration["VCAP_SERVICES"], "auditlog", "audit-log");
if (auditBinding is not null)
{
    builder.Services.AddSingleton(auditBinding);
    builder.Services.AddHttpClient<SapAuditLogSink>();
    builder.Services.AddScoped<IAuditSink>(sp => sp.GetRequiredService<SapAuditLogSink>());
}

var app = builder.Build();

if (HanaConnection.UsesInMemory(app.Configuration, app.Environment))
{
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<HireLensDbContext>();
    await db.Database.EnsureCreatedAsync();
}

app.UseSerilogRequestLogging();
app.UseAuthentication();
app.UseMiddleware<TenantResolutionMiddleware>();
app.UseAuthorization();

if (DevAuth.IsEnabled(app.Environment, app.Configuration))
{
    app.MapDevToken();
}

app.MapOpenApi().AllowAnonymous();
app.MapHireLensHealth();
app.MapMeEndpoints();
app.MapSeedEndpoints();
app.MapTenancyModule();
app.MapIdentityModule();
app.MapRecruitingModule();
app.MapCandidateModule();
app.MapDocumentsModule();
app.MapMatchingModule();
app.MapReviewModule();
app.MapComplianceModule();
app.MapNotificationModule();
app.MapMeteringModule();
app.MapAnalyticsModule();
app.MapConfigurationModule();
app.MapIntegrationModule();
app.MapInterviewModule();

app.Run();

public partial class Program;

public static class ModuleLoad
{
    public static void Ensure()
    {
        _ = typeof(TenancyModule);
        _ = typeof(IdentityModule);
        _ = typeof(RecruitingModule);
        _ = typeof(CandidateModule);
        _ = typeof(DocumentsModule);
        _ = typeof(EvidenceModule);
        _ = typeof(MatchingModule);
        _ = typeof(ReviewModule);
        _ = typeof(ComplianceModule);
        _ = typeof(TaxonomyModule);
        _ = typeof(PrivacyModule);
        _ = typeof(NotificationModule);
        _ = typeof(MeteringModule);
        _ = typeof(AnalyticsModule);
        _ = typeof(ConfigurationModule);
        _ = typeof(IntegrationModule);
        _ = typeof(InterviewModule);
    }
}
