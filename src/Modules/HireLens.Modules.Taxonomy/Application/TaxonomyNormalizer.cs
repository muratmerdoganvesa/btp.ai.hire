using HireLens.Contracts.Taxonomy;
using HireLens.Infrastructure.Persistence;
using HireLens.Modules.Taxonomy.Domain;
using HireLens.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace HireLens.Modules.Taxonomy.Application;

public sealed class TaxonomyNormalizer(HireLensDbContext db, ITenantContext tenant) : ITaxonomyNormalizer
{
    private static readonly Dictionary<string, string> BuiltIn = new(StringComparer.OrdinalIgnoreCase)
    {
        ["js"] = "JavaScript",
        ["ts"] = "TypeScript",
        ["csharp"] = "C#",
        ["c#"] = "C#",
        ["dotnet"] = ".NET",
        [".net"] = ".NET"
    };

    public async Task<IReadOnlyList<NormalizedSkill>> NormalizeAsync(
        IReadOnlyList<string> rawSkills,
        CancellationToken cancellationToken)
    {
        RepositoryGuard.RequireTenant(tenant);
        var result = new List<NormalizedSkill>();
        foreach (var raw in rawSkills.Where(s => !string.IsNullOrWhiteSpace(s)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var key = raw.Trim().ToLowerInvariant();
            var cached = await db.Set<SkillTerm>().SingleOrDefaultAsync(s => s.RawName == key, cancellationToken);
            if (cached is not null)
            {
                result.Add(new NormalizedSkill(cached.CanonicalName, raw));
                continue;
            }

            var canonical = BuiltIn.GetValueOrDefault(key, raw.Trim());
            db.Set<SkillTerm>().Add(SkillTerm.Map(tenant.TenantId, key, canonical));
            result.Add(new NormalizedSkill(canonical, raw));
        }

        await db.SaveChangesAsync(cancellationToken);
        return result;
    }
}
