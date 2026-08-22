namespace HireLens.Contracts.Recruiting;

public sealed record PositionCriterionDto(Guid Id, string Name, string Description, int Weight);

public sealed record PositionDto(
    Guid Id,
    string Title,
    string JobDescription,
    IReadOnlyList<PositionCriterionDto> Criteria,
    DateTimeOffset CreatedAt);

public sealed record UpsertPositionRequest(
    string Title,
    string JobDescription,
    IReadOnlyList<UpsertCriterionRequest> Criteria);

public sealed record UpsertCriterionRequest(string Name, string Description, int Weight);

public sealed record PositionSnapshot(
    Guid Id,
    string Title,
    string JobDescription,
    IReadOnlyList<PositionCriterionDto> Criteria);

public interface IPositionReadPort
{
    Task<PositionSnapshot?> GetAsync(Guid positionId, CancellationToken cancellationToken);
}

public interface IPositionWritePort
{
    Task<SharedKernel.Result<PositionDto>> CreateAsync(UpsertPositionRequest request, CancellationToken cancellationToken);
}
