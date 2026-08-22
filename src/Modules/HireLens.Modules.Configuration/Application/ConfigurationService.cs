using HireLens.Contracts.Analytics;
using HireLens.Contracts.Configuration;
using HireLens.Contracts.Identity;
using HireLens.Contracts.Metering;
using HireLens.Contracts.Tenancy;
using HireLens.Infrastructure.Persistence;
using HireLens.Modules.Configuration.Domain;
using HireLens.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace HireLens.Modules.Configuration.Application;

public interface IConfigurationService
{
    Task<Result<ThemeDto>> GetThemeAsync(CancellationToken cancellationToken);

    Task<Result<ThemeDto>> UpdateThemeAsync(ThemeDto request, CancellationToken cancellationToken);

    Task<Result<IReadOnlyList<RubricTemplateDto>>> ListRubricsAsync(CancellationToken cancellationToken);

    Task<Result<RubricTemplateDto>> CreateRubricAsync(UpsertRubricRequest request, CancellationToken cancellationToken);

    Task<Result<IReadOnlyList<ModelPolicyDto>>> ListPoliciesAsync(CancellationToken cancellationToken);

    Task<Result<ModelPolicyDto>> UpsertPolicyAsync(UpsertModelPolicyRequest request, CancellationToken cancellationToken);

    Task<Result<IReadOnlyList<PromptOverrideDto>>> ListPromptsAsync(CancellationToken cancellationToken);

    Task<Result<PromptOverrideDto>> UpsertPromptAsync(UpsertPromptOverrideRequest request, CancellationToken cancellationToken);

    Task<Result<ProvisionTenantResult>> ProvisionAsync(ProvisionTenantRequest request, CancellationToken cancellationToken);
}

public sealed class ConfigurationService(
    HireLensDbContext db,
    ITenantContext tenant,
    TenantContext tenantState,
    SystemTenantScope system,
    ITenantProvisionPort tenants,
    IUserCreatePort users,
    IQuotaBootstrap quotas,
    IEnumerable<IPromptExperimentPort> experiments) : IConfigurationService, IPromptCatalog, IThemeReader, IInterviewWeightPolicy
{
    public async Task<Result<ThemeDto>> GetThemeAsync(CancellationToken cancellationToken) =>
        Result.Success(await LoadThemeAsync(cancellationToken));

    public async Task<ThemeDto> GetAsync(CancellationToken cancellationToken) =>
        await LoadThemeAsync(cancellationToken);

    public async Task<int> GetInterviewWeightAsync(CancellationToken cancellationToken)
    {
        var theme = await EnsureThemeAsync(cancellationToken);
        return theme.InterviewWeight;
    }

    public async Task<Result<ThemeDto>> UpdateThemeAsync(ThemeDto request, CancellationToken cancellationToken)
    {
        RepositoryGuard.RequireTenant(tenant);
        var theme = await EnsureThemeAsync(cancellationToken);
        var applied = theme.Apply(request.BrandHue, request.LogoUrl, request.RadiusScale, request.InterviewWeight);
        if (applied.IsFailure)
        {
            return Result.Failure<ThemeDto>(applied.Error);
        }

        await db.SaveChangesAsync(cancellationToken);
        return Result.Success(ToTheme(theme));
    }

    public async Task<Result<IReadOnlyList<RubricTemplateDto>>> ListRubricsAsync(CancellationToken cancellationToken)
    {
        RepositoryGuard.RequireTenant(tenant);
        var rows = await db.Set<RubricTemplate>().ToListAsync(cancellationToken);
        return Result.Success<IReadOnlyList<RubricTemplateDto>>(rows.Select(ToRubric).ToList());
    }

    public async Task<Result<RubricTemplateDto>> CreateRubricAsync(UpsertRubricRequest request, CancellationToken cancellationToken)
    {
        RepositoryGuard.RequireTenant(tenant);
        var created = RubricTemplate.Create(
            tenant.TenantId,
            request.Name,
            request.Criteria.Select(c => (c.Name, c.Description, c.Weight)).ToList());
        if (created.IsFailure)
        {
            return Result.Failure<RubricTemplateDto>(created.Error);
        }

        db.Set<RubricTemplate>().Add(created.Value);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success(ToRubric(created.Value));
    }

    public async Task<Result<IReadOnlyList<ModelPolicyDto>>> ListPoliciesAsync(CancellationToken cancellationToken)
    {
        RepositoryGuard.RequireTenant(tenant);
        var rows = await db.Set<ModelPolicy>().ToListAsync(cancellationToken);
        return Result.Success<IReadOnlyList<ModelPolicyDto>>(rows.Select(p => new ModelPolicyDto(p.TaskType, p.ModelId, p.Region)).ToList());
    }

    public async Task<Result<ModelPolicyDto>> UpsertPolicyAsync(UpsertModelPolicyRequest request, CancellationToken cancellationToken)
    {
        RepositoryGuard.RequireTenant(tenant);
        var row = await db.Set<ModelPolicy>().SingleOrDefaultAsync(p => p.TaskType == request.TaskType, cancellationToken);
        if (row is null)
        {
            row = ModelPolicy.Set(tenant.TenantId, request.TaskType, request.ModelId, request.Region);
            db.Set<ModelPolicy>().Add(row);
        }
        else
        {
            row.Replace(request.ModelId, request.Region);
        }

        await db.SaveChangesAsync(cancellationToken);
        return Result.Success(new ModelPolicyDto(row.TaskType, row.ModelId, row.Region));
    }

    public async Task<Result<IReadOnlyList<PromptOverrideDto>>> ListPromptsAsync(CancellationToken cancellationToken)
    {
        RepositoryGuard.RequireTenant(tenant);
        var rows = await db.Set<PromptOverride>().ToListAsync(cancellationToken);
        return Result.Success<IReadOnlyList<PromptOverrideDto>>(rows.Select(p => new PromptOverrideDto(p.TaskType, p.Version, p.Body)).ToList());
    }

    public async Task<Result<PromptOverrideDto>> UpsertPromptAsync(UpsertPromptOverrideRequest request, CancellationToken cancellationToken)
    {
        RepositoryGuard.RequireTenant(tenant);
        var row = await db.Set<PromptOverride>().SingleOrDefaultAsync(p => p.TaskType == request.TaskType, cancellationToken);
        if (row is null)
        {
            row = PromptOverride.Create(tenant.TenantId, request.TaskType, request.Version, request.Body);
            db.Set<PromptOverride>().Add(row);
        }
        else
        {
            row.Replace(request.Version, request.Body);
        }

        await db.SaveChangesAsync(cancellationToken);
        return Result.Success(new PromptOverrideDto(row.TaskType, row.Version, row.Body));
    }

    public async Task<PromptSelection> ResolveAsync(string taskType, string subjectKey, CancellationToken cancellationToken)
    {
        var experiment = experiments.FirstOrDefault();
        var assigned = experiment is null
            ? null
            : await experiment.AssignVersionAsync(taskType, subjectKey, cancellationToken);
        var overrideRow = tenant.IsResolved
            ? await db.Set<PromptOverride>().SingleOrDefaultAsync(p => p.TaskType == taskType, cancellationToken)
            : null;
        if (overrideRow is not null && (assigned is null || assigned == overrideRow.Version))
        {
            return new PromptSelection(overrideRow.Version, overrideRow.Body);
        }

        var file = Path.Combine(AppContext.BaseDirectory, "prompts", taskType, $"{assigned ?? "v1"}.md");
        if (!File.Exists(file))
        {
            file = Path.Combine(AppContext.BaseDirectory, "prompts", taskType, "v1.md");
        }

        var body = File.Exists(file) ? await File.ReadAllTextAsync(file, cancellationToken) : $"# {taskType}";
        return new PromptSelection(assigned ?? "v1", body);
    }

    public async Task<Result<ProvisionTenantResult>> ProvisionAsync(ProvisionTenantRequest request, CancellationToken cancellationToken)
    {
        using (system.Use(request.TenantId, "provision"))
        {
            tenantState.Resolve(request.TenantId, "system", "provision");
            var tenantResult = await tenants.ProvisionAsync(request.TenantId, request.Name, request.Slug, cancellationToken);
            if (tenantResult.IsFailure && tenantResult.Error.Code != "conflict")
            {
                return Result.Failure<ProvisionTenantResult>(tenantResult.Error);
            }

            var user = await users.CreateAsync(
                new CreateUserRequest(request.AdminSubject, request.Name + " admin", ["TenantAdmin"]),
                cancellationToken);
            if (user.IsFailure && user.Error.Code != "conflict")
            {
                return Result.Failure<ProvisionTenantResult>(user.Error);
            }

            if (!await db.Set<TenantTheme>().AnyAsync(cancellationToken))
            {
                db.Set<TenantTheme>().Add(TenantTheme.Default(request.TenantId));
            }

            await quotas.EnsureDefaultAsync(cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            return Result.Success(new ProvisionTenantResult(request.TenantId, request.Slug, request.AdminSubject));
        }
    }

    private async Task<ThemeDto> LoadThemeAsync(CancellationToken cancellationToken)
    {
        var theme = tenant.IsResolved ? await EnsureThemeAsync(cancellationToken) : TenantTheme.Default(Guid.Empty);
        return ToTheme(theme);
    }

    private async Task<TenantTheme> EnsureThemeAsync(CancellationToken cancellationToken)
    {
        RepositoryGuard.RequireTenant(tenant);
        var theme = await db.Set<TenantTheme>().SingleOrDefaultAsync(cancellationToken);
        if (theme is not null)
        {
            return theme;
        }

        theme = TenantTheme.Default(tenant.TenantId);
        db.Set<TenantTheme>().Add(theme);
        await db.SaveChangesAsync(cancellationToken);
        return theme;
    }

    private static ThemeDto ToTheme(TenantTheme theme)
    {
        var contrastOk = Contrast.IsAa(theme.BrandHue);
        return new ThemeDto(theme.BrandHue, theme.LogoUrl, theme.RadiusScale, contrastOk, theme.InterviewWeight);
    }

    private static RubricTemplateDto ToRubric(RubricTemplate template) =>
        new(template.Id, template.Name, template.Criteria.Select(c => new RubricCriterionDto(c.Name, c.Description, c.Weight)).ToList());
}

public static class Contrast
{
    public static bool IsAa(int brandHue)
    {
        if (brandHue is < 0 or > 360)
        {
            return false;
        }

        // Token recipe: page surface vs body ink. OKLCH L is the lightness axis.
        var surface = OklchRelativeLuminance(0.99, 0.005, brandHue);
        var ink = OklchRelativeLuminance(0.18, 0.02, brandHue);
        var ratio = (Math.Max(surface, ink) + 0.05) / (Math.Min(surface, ink) + 0.05);
        return ratio >= 4.5;
    }

    private static double OklchRelativeLuminance(double l, double c, int hue)
    {
        _ = c;
        _ = hue;
        return Math.Clamp(l, 0, 1);
    }
}
