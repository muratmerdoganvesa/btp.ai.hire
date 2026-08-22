using HireLens.SharedKernel;

namespace HireLens.Modules.Metering.Domain;

public sealed class TenantQuota : ITenantEntity
{
    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public int MonthlyTokenLimit { get; private set; } = 200_000;

    public string OveragePolicy { get; private set; } = "allow";

    public static TenantQuota Default(Guid tenantId) =>
        new() { Id = Guid.NewGuid(), TenantId = tenantId };

    public void Configure(int monthlyTokenLimit, string overagePolicy)
    {
        MonthlyTokenLimit = monthlyTokenLimit;
        OveragePolicy = overagePolicy is "block" or "allow" ? overagePolicy : "allow";
    }
}
