using Hangfire;
using Hangfire.InMemory;
using HireLens.Infrastructure.Btp;
using HireLens.Infrastructure.Hosting;
using HireLens.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;
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
_ = typeof(InterviewModule);
_ = typeof(NotificationModule);
_ = typeof(ConfigurationModule);
_ = typeof(MeteringModule);
_ = typeof(IntegrationModule);
_ = typeof(AnalyticsModule);

var builder = WebApplication.CreateBuilder(args);

var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(port))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}

builder.Host.UseSerilog((context, logger) =>
    logger.ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("service", "hirelens-worker")
        .WriteTo.Console());

builder.Services.AddHireLensPersistence(builder.Configuration, builder.Environment);
builder.Services.AddTenancyModule();
builder.Services.AddIdentityModule();
builder.Services.AddHealthChecks()
    .AddDbContextCheck<HireLensDbContext>("database", tags: ["ready"]);

builder.Services.AddHangfire(config => config.UseInMemoryStorage());
builder.Services.AddHangfireServer();

var app = builder.Build();

// Shared HANA schema — same bootstrap as hirelens-api; never crash the worker process.
if (!app.Environment.IsEnvironment("Testing")
    && (HanaConnection.UsesInMemory(app.Configuration, app.Environment)
        || !string.IsNullOrWhiteSpace(HanaConnection.Resolve(app.Configuration))))
{
    try
    {
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HireLensDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("SchemaBootstrap");
        await SchemaBootstrap.EnsureApplicationTablesAsync(db, logger);
        await SchemaBootstrap.EnsureAuditTablesAsync(db, logger);
        await SchemaBootstrap.EnsureDocumentPipelineTablesAsync(db, logger);
        await SchemaBootstrap.EnsurePrivacyTablesAsync(db, logger);
        await SchemaBootstrap.EnsureEvaluationAuditColumnsAsync(db, logger);
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Schema bootstrap failed; worker will start and report DB via /health/ready.");
    }
}

app.UseSerilogRequestLogging();
app.MapHireLensHealth();

app.Run();
