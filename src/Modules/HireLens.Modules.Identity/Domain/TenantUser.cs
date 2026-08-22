using HireLens.SharedKernel;

namespace HireLens.Modules.Identity.Domain;

public sealed class TenantUser : ITenantEntity
{
    private readonly List<TenantUserRole> _roles = [];

    private TenantUser()
    {
        ExternalSubject = string.Empty;
        DisplayName = string.Empty;
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public string ExternalSubject { get; private set; }

    public string DisplayName { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public IReadOnlyCollection<TenantUserRole> Roles => _roles;

    public static Result<TenantUser> Create(
        Guid tenantId,
        string externalSubject,
        string displayName,
        IReadOnlyCollection<string> roles,
        DateTimeOffset createdAt)
    {
        if (string.IsNullOrWhiteSpace(externalSubject))
        {
            return Result.Failure<TenantUser>(Error.Validation("External subject is required."));
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            return Result.Failure<TenantUser>(Error.Validation("Display name is required."));
        }

        var user = new TenantUser
        {
            Id = Guid.NewGuid(),
            TenantId = Guard.NotEmpty(tenantId, nameof(tenantId)),
            ExternalSubject = externalSubject.Trim(),
            DisplayName = displayName.Trim(),
            CreatedAt = createdAt
        };

        var assigned = user.ReplaceRoles(roles);
        return assigned.IsFailure ? Result.Failure<TenantUser>(assigned.Error) : Result.Success(user);
    }

    public Result Rename(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            return Result.Failure(Error.Validation("Display name is required."));
        }

        DisplayName = displayName.Trim();
        return Result.Success();
    }

    public Result ReplaceRoles(IReadOnlyCollection<string> roles)
    {
        if (roles.Count == 0)
        {
            return Result.Failure(Error.Validation("At least one role is required."));
        }

        foreach (var role in roles)
        {
            if (!Contracts.Roles.All.Contains(role))
            {
                return Result.Failure(Error.Validation($"Unknown role '{role}'."));
            }
        }

        _roles.Clear();
        foreach (var role in roles.Distinct(StringComparer.Ordinal))
        {
            _roles.Add(TenantUserRole.Assign(TenantId, Id, role));
        }

        return Result.Success();
    }
}

public sealed class TenantUserRole : ITenantEntity
{
    private TenantUserRole()
    {
        RoleName = string.Empty;
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid UserId { get; private set; }

    public string RoleName { get; private set; }

    public static TenantUserRole Assign(Guid tenantId, Guid userId, string roleName) =>
        new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserId = userId,
            RoleName = roleName
        };
}
