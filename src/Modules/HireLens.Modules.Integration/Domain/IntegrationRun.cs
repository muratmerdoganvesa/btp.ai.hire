using HireLens.SharedKernel;

namespace HireLens.Modules.Integration.Domain;

public sealed class IntegrationRun : ITenantEntity
{
    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public string System { get; private set; } = "successfactors";

    public string Status { get; private set; } = "succeeded";

    public int Imported { get; private set; }

    public DateTimeOffset RanAt { get; private set; }

    public static IntegrationRun Complete(Guid tenantId, string system, int imported, DateTimeOffset now) =>
        new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            System = system,
            Status = "succeeded",
            Imported = imported,
            RanAt = now
        };
}
