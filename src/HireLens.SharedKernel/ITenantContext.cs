namespace HireLens.SharedKernel;

/// <summary>
/// Request-scoped tenant identity. Repositories must refuse to run when this
/// is unresolved — a missing tenant is a security fault, not a default.
/// </summary>
public interface ITenantContext
{
    Guid TenantId { get; }

    bool IsResolved { get; }

    string? ActorSubject { get; }

    string? CorrelationId { get; }
}

public sealed class TenantContext : ITenantContext
{
    public Guid TenantId { get; private set; }

    public bool IsResolved { get; private set; }

    public string? ActorSubject { get; private set; }

    public string? CorrelationId { get; private set; }

    public void Resolve(Guid tenantId, string? actorSubject, string? correlationId)
    {
        TenantId = Guard.NotEmpty(tenantId, nameof(tenantId));
        ActorSubject = actorSubject;
        CorrelationId = correlationId;
        IsResolved = true;
    }

    public IDisposable EnterSystemScope(Guid tenantId, string correlationId)
    {
        var previous = (TenantId, IsResolved, ActorSubject, CorrelationId);
        Resolve(tenantId, "system", correlationId);
        return new Scope(() =>
        {
            TenantId = previous.TenantId;
            IsResolved = previous.IsResolved;
            ActorSubject = previous.ActorSubject;
            CorrelationId = previous.CorrelationId;
        });
    }

    private sealed class Scope(Action restore) : IDisposable
    {
        public void Dispose() => restore();
    }
}

/// <summary>
/// Explicit, auditable bypass for background jobs. Silent tenant-filter
/// disable is forbidden — every system write names the tenant it acts as.
/// </summary>
public sealed class SystemTenantScope
{
    private readonly TenantContext _context;

    public SystemTenantScope(ITenantContext context)
    {
        _context = context as TenantContext
            ?? throw new InvalidOperationException("SystemTenantScope requires the default TenantContext.");
    }

    public IDisposable Use(Guid tenantId, string correlationId) =>
        _context.EnterSystemScope(tenantId, correlationId);
}
