using HireLens.SharedKernel;

namespace HireLens.Modules.Configuration.Domain;

public sealed class TenantTheme : ITenantEntity
{
    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public int BrandHue { get; private set; } = 250;

    public string? LogoUrl { get; private set; }

    public double RadiusScale { get; private set; } = 1;

    public int InterviewWeight { get; private set; } = 30;

    public static TenantTheme Default(Guid tenantId) =>
        new() { Id = Guid.NewGuid(), TenantId = tenantId };

    public Result Apply(int brandHue, string? logoUrl, double radiusScale, int interviewWeight)
    {
        if (brandHue is < 0 or > 360)
        {
            return Result.Failure(Error.Validation("Brand hue must be between 0 and 360."));
        }

        if (interviewWeight is < 0 or > 100)
        {
            return Result.Failure(Error.Validation("Interview weight must be between 0 and 100."));
        }

        BrandHue = brandHue;
        LogoUrl = logoUrl;
        RadiusScale = Math.Clamp(radiusScale, 0.5, 2);
        InterviewWeight = interviewWeight;
        return Result.Success();
    }
}

public sealed class RubricTemplate : ITenantEntity
{
    private readonly List<RubricTemplateCriterion> _criteria = [];

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public IReadOnlyCollection<RubricTemplateCriterion> Criteria => _criteria;

    public static Result<RubricTemplate> Create(Guid tenantId, string name, IReadOnlyList<(string Name, string Description, int Weight)> criteria)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure<RubricTemplate>(Error.Validation("Rubric name is required."));
        }

        if (criteria.Count == 0 || criteria.Sum(c => c.Weight) != 100)
        {
            return Result.Failure<RubricTemplate>(Error.Validation("Criterion weights must sum to 100."));
        }

        var row = new RubricTemplate { Id = Guid.NewGuid(), TenantId = tenantId, Name = name.Trim() };
        foreach (var criterion in criteria)
        {
            row._criteria.Add(RubricTemplateCriterion.Create(tenantId, row.Id, criterion.Name, criterion.Description, criterion.Weight));
        }

        return Result.Success(row);
    }
}

public sealed class RubricTemplateCriterion : ITenantEntity
{
    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid TemplateId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    public int Weight { get; private set; }

    public static RubricTemplateCriterion Create(Guid tenantId, Guid templateId, string name, string description, int weight) =>
        new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            TemplateId = templateId,
            Name = name.Trim(),
            Description = description.Trim(),
            Weight = weight
        };
}

public sealed class ModelPolicy : ITenantEntity
{
    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public string TaskType { get; private set; } = string.Empty;

    public string ModelId { get; private set; } = string.Empty;

    public string? Region { get; private set; }

    public static ModelPolicy Set(Guid tenantId, string taskType, string modelId, string? region) =>
        new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            TaskType = taskType,
            ModelId = modelId,
            Region = region
        };

    public void Replace(string modelId, string? region)
    {
        ModelId = modelId;
        Region = region;
    }
}

public sealed class PromptOverride : ITenantEntity
{
    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public string TaskType { get; private set; } = string.Empty;

    public string Version { get; private set; } = string.Empty;

    public string Body { get; private set; } = string.Empty;

    public static PromptOverride Create(Guid tenantId, string taskType, string version, string body) =>
        new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            TaskType = taskType,
            Version = version,
            Body = body
        };

    public void Replace(string version, string body)
    {
        Version = version;
        Body = body;
    }
}
