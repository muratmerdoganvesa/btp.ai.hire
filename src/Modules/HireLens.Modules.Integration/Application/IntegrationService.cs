using HireLens.Contracts.Candidates;
using HireLens.Contracts.Integration;
using HireLens.Contracts.Recruiting;
using HireLens.Infrastructure.Persistence;
using HireLens.Modules.Integration.Domain;
using HireLens.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace HireLens.Modules.Integration.Application;

public interface IIntegrationService
{
    Task<Result<IntegrationRunDto>> SyncSuccessFactorsAsync(
        IReadOnlyList<SfPositionSync> positions,
        IReadOnlyList<SfCandidateSync> candidates,
        CancellationToken cancellationToken);

    Task<Result<IReadOnlyList<IntegrationRunDto>>> ListAsync(CancellationToken cancellationToken);
}

public sealed class IntegrationService(
    HireLensDbContext db,
    ITenantContext tenant,
    IClock clock,
    IPositionWritePort positions,
    ICandidateWritePort candidates) : IIntegrationService
{
    public async Task<Result<IntegrationRunDto>> SyncSuccessFactorsAsync(
        IReadOnlyList<SfPositionSync> incomingPositions,
        IReadOnlyList<SfCandidateSync> incomingCandidates,
        CancellationToken cancellationToken)
    {
        RepositoryGuard.RequireTenant(tenant);
        var imported = 0;
        var positionMap = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in incomingPositions)
        {
            var created = await positions.CreateAsync(
                new UpsertPositionRequest(
                    item.Title,
                    item.JobDescription,
                    [new UpsertCriterionRequest("Core", item.Title, 100)]),
                cancellationToken);
            if (created.IsSuccess)
            {
                positionMap[item.ExternalId] = created.Value.Id;
                imported++;
            }
        }

        foreach (var item in incomingCandidates)
        {
            if (!positionMap.TryGetValue(item.PositionExternalId, out var positionId))
            {
                continue;
            }

            var created = await candidates.CreateAsync(positionId, new CreateCandidateRequest(item.DisplayName), cancellationToken);
            if (created.IsSuccess)
            {
                imported++;
            }
        }

        var run = IntegrationRun.Complete(tenant.TenantId, "successfactors", imported, clock.UtcNow);
        db.Set<IntegrationRun>().Add(run);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success(new IntegrationRunDto(run.Id, run.System, run.Status, run.Imported, run.RanAt));
    }

    public async Task<Result<IReadOnlyList<IntegrationRunDto>>> ListAsync(CancellationToken cancellationToken)
    {
        RepositoryGuard.RequireTenant(tenant);
        var rows = await db.Set<IntegrationRun>().OrderByDescending(r => r.RanAt).ToListAsync(cancellationToken);
        return Result.Success<IReadOnlyList<IntegrationRunDto>>(
            rows.Select(r => new IntegrationRunDto(r.Id, r.System, r.Status, r.Imported, r.RanAt)).ToList());
    }
}
