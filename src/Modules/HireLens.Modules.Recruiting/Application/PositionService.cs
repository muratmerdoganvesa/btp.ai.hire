using HireLens.Contracts.Recruiting;
using HireLens.Infrastructure.Persistence;
using HireLens.Modules.Recruiting.Domain;
using HireLens.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace HireLens.Modules.Recruiting.Application;

public interface IPositionService
{
    Task<Result<IReadOnlyList<PositionDto>>> ListAsync(CancellationToken cancellationToken);

    Task<Result<PositionDto>> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<Result<PositionDto>> CreateAsync(UpsertPositionRequest request, CancellationToken cancellationToken);

    Task<Result<PositionDto>> UpdateAsync(Guid id, UpsertPositionRequest request, CancellationToken cancellationToken);
}

public sealed class PositionService(
    HireLensDbContext db,
    ITenantContext tenant,
    IClock clock) : IPositionService, IPositionReadPort, IPositionWritePort
{
    public async Task<Result<IReadOnlyList<PositionDto>>> ListAsync(CancellationToken cancellationToken)
    {
        RepositoryGuard.RequireTenant(tenant);
        var rows = await db.Set<Position>().ToListAsync(cancellationToken);
        return Result.Success<IReadOnlyList<PositionDto>>(rows.Select(ToDto).ToList());
    }

    public async Task<Result<PositionDto>> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        RepositoryGuard.RequireTenant(tenant);
        var row = await db.Set<Position>().SingleOrDefaultAsync(p => p.Id == id, cancellationToken);
        return row is null
            ? Result.Failure<PositionDto>(Error.NotFound("Position was not found."))
            : Result.Success(ToDto(row));
    }

    public async Task<Result<PositionDto>> CreateAsync(UpsertPositionRequest request, CancellationToken cancellationToken)
    {
        RepositoryGuard.RequireTenant(tenant);
        var created = Position.Create(
            tenant.TenantId,
            request.Title,
            request.JobDescription,
            request.Criteria.Select(c => (c.Name, c.Description, c.Weight)).ToList(),
            clock.UtcNow);
        if (created.IsFailure)
        {
            return Result.Failure<PositionDto>(created.Error);
        }

        db.Set<Position>().Add(created.Value);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success(ToDto(created.Value));
    }

    public async Task<Result<PositionDto>> UpdateAsync(Guid id, UpsertPositionRequest request, CancellationToken cancellationToken)
    {
        RepositoryGuard.RequireTenant(tenant);
        var row = await db.Set<Position>().SingleOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (row is null)
        {
            return Result.Failure<PositionDto>(Error.NotFound("Position was not found."));
        }

        var renamed = row.Rename(request.Title, request.JobDescription);
        if (renamed.IsFailure)
        {
            return Result.Failure<PositionDto>(renamed.Error);
        }

        var criteria = row.ReplaceCriteria(request.Criteria.Select(c => (c.Name, c.Description, c.Weight)).ToList());
        if (criteria.IsFailure)
        {
            return Result.Failure<PositionDto>(criteria.Error);
        }

        await db.SaveChangesAsync(cancellationToken);
        return Result.Success(ToDto(row));
    }

    async Task<PositionSnapshot?> IPositionReadPort.GetAsync(Guid positionId, CancellationToken cancellationToken)
    {
        var result = await GetAsync(positionId, cancellationToken);
        return result.IsFailure
            ? null
            : new PositionSnapshot(result.Value.Id, result.Value.Title, result.Value.JobDescription, result.Value.Criteria);
    }

    private static PositionDto ToDto(Position position) =>
        new(
            position.Id,
            position.Title,
            position.JobDescription,
            position.Criteria.Select(c => new PositionCriterionDto(c.Id, c.Name, c.Description, c.Weight)).ToList(),
            position.CreatedAt);
}
