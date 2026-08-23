using System.Text;
using HireLens.AiGateway.Masking;
using HireLens.Infrastructure.Persistence;
using HireLens.Infrastructure.Seed;
using HireLens.Infrastructure.Storage;
using HireLens.Modules.Candidate.Domain;
using HireLens.Modules.Documents.Domain;
using HireLens.Modules.Recruiting.Domain;
using HireLens.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace HireLens.Api.Seed;

public sealed record DemoSeedResult(
    bool Skipped,
    int Positions,
    int Candidates,
    int Documents);

public interface IDemoSeedService
{
    Task<Result<DemoSeedResult>> SeedAsync(CancellationToken cancellationToken);
}

public sealed class DemoSeedService(
    HireLensDbContext db,
    ITenantContext tenant,
    IClock clock,
    IObjectStore objectStore,
    IPiiMasker masker) : IDemoSeedService
{
    public async Task<Result<DemoSeedResult>> SeedAsync(CancellationToken cancellationToken)
    {
        RepositoryGuard.RequireTenant(tenant);

        var titles = await db.Set<Position>().Select(p => p.Title).ToListAsync(cancellationToken);
        if (titles.Exists(title => title.StartsWith(DemoCvCatalog.TitlePrefix, StringComparison.Ordinal)))
        {
            return Result.Success(new DemoSeedResult(true, 0, 0, 0));
        }

        var positions = 0;
        var candidates = 0;
        var documents = 0;
        var byTitle = new Dictionary<string, Position>(StringComparer.Ordinal);

        foreach (var blueprint in DemoCvCatalog.Positions)
        {
            var created = Position.Create(
                tenant.TenantId,
                blueprint.Title,
                blueprint.JobDescription,
                blueprint.Criteria,
                clock.UtcNow);
            if (created.IsFailure)
            {
                return Result.Failure<DemoSeedResult>(created.Error);
            }

            db.Set<Position>().Add(created.Value);
            byTitle[blueprint.Title] = created.Value;
            positions++;
        }

        await db.SaveChangesAsync(cancellationToken);

        foreach (var group in DemoCvCatalog.Cvs.GroupBy(cv => cv.PositionTitle))
        {
            var position = byTitle[group.Key];
            foreach (var cv in group)
            {
                var person = Candidate.Create(tenant.TenantId, position.Id, cv.CandidateName, clock.UtcNow);
                if (person.IsFailure)
                {
                    return Result.Failure<DemoSeedResult>(person.Error);
                }

                person.Value.MarkReady();
                db.Set<Candidate>().Add(person.Value);

                var bytes = Encoding.UTF8.GetBytes(cv.Text);
                var document = CvDocument.Create(
                    tenant.TenantId,
                    person.Value.Id,
                    position.Id,
                    cv.FileName,
                    "text/plain",
                    bytes.LongLength,
                    clock.UtcNow);
                if (document.IsFailure)
                {
                    return Result.Failure<DemoSeedResult>(document.Error);
                }

                await using var stream = new MemoryStream(bytes);
                await objectStore.PutAsync(document.Value.ObjectKey, stream, "text/plain", cancellationToken);
                document.Value.MarkUploaded();
                document.Value.MarkParsed(masker.Mask(cv.Text).Text, "seed-v1");
                db.Set<CvDocument>().Add(document.Value);
                candidates++;
                documents++;
            }

            await db.SaveChangesAsync(cancellationToken);
        }

        return Result.Success(new DemoSeedResult(false, positions, candidates, documents));
    }
}
