using HireLens.Contracts.Identity;
using HireLens.Infrastructure.Persistence;
using HireLens.Modules.Identity.Domain;
using HireLens.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace HireLens.Modules.Identity.Application;

public interface IUserService
{
    Task<Result<IReadOnlyList<UserDto>>> ListAsync(CancellationToken cancellationToken);

    Task<Result<UserDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<Result<UserDto>> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken);

    Task<Result<UserDto>> UpdateAsync(Guid id, UpdateUserRequest request, CancellationToken cancellationToken);

    Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken);
}

public sealed class UserService(
    HireLensDbContext db,
    ITenantContext tenantContext,
    IClock clock) : IUserService, IUserCreatePort
{
    public async Task<Result<IReadOnlyList<UserDto>>> ListAsync(CancellationToken cancellationToken)
    {
        RepositoryGuard.RequireTenant(tenantContext);
        var users = await db.Set<TenantUser>().ToListAsync(cancellationToken);
        return Result.Success<IReadOnlyList<UserDto>>(users.Select(ToDto).ToList());
    }

    public async Task<Result<UserDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        RepositoryGuard.RequireTenant(tenantContext);
        var user = await db.Set<TenantUser>().SingleOrDefaultAsync(u => u.Id == id, cancellationToken);
        return user is null
            ? Result.Failure<UserDto>(Error.NotFound("User was not found."))
            : Result.Success(ToDto(user));
    }

    public async Task<Result<UserDto>> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken)
    {
        RepositoryGuard.RequireTenant(tenantContext);

        var exists = await db.Set<TenantUser>()
            .AnyAsync(u => u.ExternalSubject == request.ExternalSubject, cancellationToken);
        if (exists)
        {
            return Result.Failure<UserDto>(Error.Conflict("A user with this subject already exists."));
        }

        var created = TenantUser.Create(
            tenantContext.TenantId,
            request.ExternalSubject,
            request.DisplayName,
            request.Roles,
            clock.UtcNow);

        if (created.IsFailure)
        {
            return Result.Failure<UserDto>(created.Error);
        }

        db.Set<TenantUser>().Add(created.Value);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success(ToDto(created.Value));
    }

    public async Task<Result<UserDto>> UpdateAsync(Guid id, UpdateUserRequest request, CancellationToken cancellationToken)
    {
        RepositoryGuard.RequireTenant(tenantContext);
        var user = await db.Set<TenantUser>().SingleOrDefaultAsync(u => u.Id == id, cancellationToken);
        if (user is null)
        {
            return Result.Failure<UserDto>(Error.NotFound("User was not found."));
        }

        var renamed = user.Rename(request.DisplayName);
        if (renamed.IsFailure)
        {
            return Result.Failure<UserDto>(renamed.Error);
        }

        var roles = user.ReplaceRoles(request.Roles);
        if (roles.IsFailure)
        {
            return Result.Failure<UserDto>(roles.Error);
        }

        await db.SaveChangesAsync(cancellationToken);
        return Result.Success(ToDto(user));
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        RepositoryGuard.RequireTenant(tenantContext);
        var user = await db.Set<TenantUser>().SingleOrDefaultAsync(u => u.Id == id, cancellationToken);
        if (user is null)
        {
            return Result.Failure(Error.NotFound("User was not found."));
        }

        db.Set<TenantUser>().Remove(user);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private static UserDto ToDto(TenantUser user) =>
        new(
            user.Id,
            user.TenantId,
            user.ExternalSubject,
            user.DisplayName,
            user.Roles.Select(r => r.RoleName).ToList(),
            user.CreatedAt);
}
