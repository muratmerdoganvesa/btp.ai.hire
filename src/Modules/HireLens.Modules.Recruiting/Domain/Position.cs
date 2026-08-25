using System.Text.RegularExpressions;
using HireLens.SharedKernel;

namespace HireLens.Modules.Recruiting.Domain;

public sealed class Position : ITenantEntity
{
    private readonly List<PositionCriterion> _criteria = [];

    private Position()
    {
        Title = string.Empty;
        JobDescription = string.Empty;
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public string Title { get; private set; }

    public string JobDescription { get; private set; }

    public string Slug { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public IReadOnlyCollection<PositionCriterion> Criteria => _criteria;

    public static Result<Position> Create(
        Guid tenantId,
        string title,
        string jobDescription,
        IReadOnlyList<(string Name, string Description, int Weight)> criteria,
        DateTimeOffset createdAt)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return Result.Failure<Position>(Error.Validation("Position title is required."));
        }

        if (string.IsNullOrWhiteSpace(jobDescription))
        {
            return Result.Failure<Position>(Error.Validation("Job description is required."));
        }

        var weights = ValidateWeights(criteria.Select(c => c.Weight).ToList());
        if (weights.IsFailure)
        {
            return Result.Failure<Position>(weights.Error);
        }

        var id = Guid.NewGuid();
        var position = new Position
        {
            Id = id,
            TenantId = tenantId,
            Title = title.Trim(),
            JobDescription = jobDescription.Trim(),
            Slug = BuildSlug(title, id),
            CreatedAt = createdAt
        };

        foreach (var criterion in criteria)
        {
            position._criteria.Add(PositionCriterion.Create(
                tenantId,
                position.Id,
                criterion.Name,
                criterion.Description,
                criterion.Weight));
        }

        return Result.Success(position);
    }

    public Result ReplaceCriteria(IReadOnlyList<(string Name, string Description, int Weight)> criteria)
    {
        var weights = ValidateWeights(criteria.Select(c => c.Weight).ToList());
        if (weights.IsFailure)
        {
            return weights;
        }

        _criteria.Clear();
        foreach (var criterion in criteria)
        {
            _criteria.Add(PositionCriterion.Create(
                TenantId,
                Id,
                criterion.Name,
                criterion.Description,
                criterion.Weight));
        }

        return Result.Success();
    }

    public Result Rename(string title, string jobDescription)
    {
        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(jobDescription))
        {
            return Result.Failure(Error.Validation("Title and job description are required."));
        }

        Title = title.Trim();
        JobDescription = jobDescription.Trim();
        return Result.Success();
    }

    internal static string BuildSlug(string title, Guid id)
    {
        var normalized = title.Trim().ToLowerInvariant();
        normalized = Regex.Replace(normalized, @"[^a-z0-9\s-]", string.Empty);
        normalized = Regex.Replace(normalized, @"\s+", "-");
        normalized = normalized.Trim('-');
        if (string.IsNullOrWhiteSpace(normalized))
        {
            normalized = "job";
        }

        return $"{normalized}-{id.ToString("N")[..8]}";
    }

    internal static Result ValidateWeights(IReadOnlyList<int> weights)
    {
        if (weights.Count == 0)
        {
            return Result.Failure(Error.Validation("At least one criterion is required."));
        }

        if (weights.Any(w => w <= 0))
        {
            return Result.Failure(Error.Validation("Each criterion weight must be greater than zero."));
        }

        if (weights.Sum() != 100)
        {
            return Result.Failure(Error.Validation("Criterion weights must sum to 100."));
        }

        return Result.Success();
    }
}

public sealed class PositionCriterion : ITenantEntity
{
    private PositionCriterion()
    {
        Name = string.Empty;
        Description = string.Empty;
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid PositionId { get; private set; }

    public string Name { get; private set; }

    public string Description { get; private set; }

    public int Weight { get; private set; }

    public static PositionCriterion Create(
        Guid tenantId,
        Guid positionId,
        string name,
        string description,
        int weight) =>
        new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PositionId = positionId,
            Name = Guard.NotNullOrWhiteSpace(name, nameof(name)).Trim(),
            Description = description?.Trim() ?? string.Empty,
            Weight = weight
        };
}
