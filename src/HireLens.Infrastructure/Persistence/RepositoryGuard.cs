using HireLens.SharedKernel;

namespace HireLens.Infrastructure.Persistence;

public static class RepositoryGuard
{
    public static void RequireTenant(ITenantContext tenantContext)
    {
        if (!tenantContext.IsResolved)
        {
            throw new InvalidOperationException(
                "A repository cannot run without a resolved tenant context.");
        }
    }
}
