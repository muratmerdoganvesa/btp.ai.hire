namespace HireLens.Contracts.Configuration;

public sealed record ThemeDto(int BrandHue, string? LogoUrl, double RadiusScale, bool ContrastOk, int InterviewWeight = 30);

public sealed record RubricTemplateDto(
    Guid Id,
    string Name,
    IReadOnlyList<RubricCriterionDto> Criteria);

public sealed record RubricCriterionDto(string Name, string Description, int Weight);

public sealed record UpsertRubricRequest(string Name, IReadOnlyList<RubricCriterionDto> Criteria);

public sealed record ModelPolicyDto(string TaskType, string ModelId, string? Region);

public sealed record UpsertModelPolicyRequest(string TaskType, string ModelId, string? Region);

public sealed record PromptOverrideDto(string TaskType, string Version, string Body);

public sealed record UpsertPromptOverrideRequest(string TaskType, string Version, string Body);

public sealed record ProvisionTenantRequest(Guid TenantId, string Name, string Slug, string AdminSubject);

public sealed record ProvisionTenantResult(Guid TenantId, string Slug, string AdminSubject);

public sealed record PromptSelection(string Version, string Body);

public interface IPromptCatalog
{
    Task<PromptSelection> ResolveAsync(string taskType, string subjectKey, CancellationToken cancellationToken);
}

public interface IThemeReader
{
    Task<ThemeDto> GetAsync(CancellationToken cancellationToken);
}

public interface IInterviewWeightPolicy
{
    Task<int> GetInterviewWeightAsync(CancellationToken cancellationToken);
}
