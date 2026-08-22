namespace HireLens.Contracts.Identity;

public sealed record UserDto(
    Guid Id,
    Guid TenantId,
    string ExternalSubject,
    string DisplayName,
    IReadOnlyList<string> Roles,
    DateTimeOffset CreatedAt);

public sealed record CreateUserRequest(
    string ExternalSubject,
    string DisplayName,
    IReadOnlyList<string> Roles);

public sealed record UpdateUserRequest(
    string DisplayName,
    IReadOnlyList<string> Roles);

public sealed record UserProvisioned(Guid TenantId, Guid UserId, string ExternalSubject) : SharedKernel.DomainEvent;

public interface IUserCreatePort
{
    Task<SharedKernel.Result<UserDto>> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken);
}
