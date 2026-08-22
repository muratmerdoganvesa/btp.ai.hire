namespace HireLens.Contracts.Taxonomy;

public sealed record NormalizedSkill(string CanonicalName, string RawName);

public interface ITaxonomyNormalizer
{
    Task<IReadOnlyList<NormalizedSkill>> NormalizeAsync(
        IReadOnlyList<string> rawSkills,
        CancellationToken cancellationToken);
}
