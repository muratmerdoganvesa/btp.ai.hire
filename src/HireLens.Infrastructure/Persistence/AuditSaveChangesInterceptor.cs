using HireLens.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace HireLens.Infrastructure.Persistence;

/// <summary>
/// Audit is produced here so a later feature cannot forget to log a write.
/// AuditEvent and AiInvocation themselves are excluded to prevent recursion.
/// </summary>
public sealed class AuditSaveChangesInterceptor(
    ITenantContext tenantContext,
    IClock clock,
    IAuditWriter auditWriter) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        AppendAuditEvents(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        AppendAuditEvents(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        FlushExternalSinks(eventData.Context);
        return base.SavedChanges(eventData, result);
    }

    public override ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        FlushExternalSinks(eventData.Context);
        return base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    private void AppendAuditEvents(DbContext? context)
    {
        if (context is null || !tenantContext.IsResolved)
        {
            return;
        }

        var now = clock.UtcNow;
        var added = new List<AuditEvent>();

        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted))
            {
                continue;
            }

            if (entry.Entity is AuditEvent or AiInvocation)
            {
                continue;
            }

            var entityId = entry.Properties.FirstOrDefault(p => p.Metadata.IsPrimaryKey())?.CurrentValue?.ToString()
                ?? "unknown";

            added.Add(new AuditEvent
            {
                Id = Guid.NewGuid(),
                TenantId = tenantContext.TenantId,
                Action = entry.State.ToString(),
                EntityType = entry.Metadata.ClrType.Name,
                EntityId = entityId,
                ActorSubject = tenantContext.ActorSubject,
                OccurredAt = now,
                CorrelationId = tenantContext.CorrelationId
            });
        }

        if (added.Count == 0)
        {
            return;
        }

        context.Set<AuditEvent>().AddRange(added);
    }

    private void FlushExternalSinks(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var pending = context.ChangeTracker
            .Entries<AuditEvent>()
            .Select(e => e.Entity)
            .ToList();

        if (pending.Count == 0)
        {
            return;
        }

        // External sinks must not block the request path on failure; local rows already persisted.
        _ = auditWriter.WriteAsync(pending, CancellationToken.None);
    }
}
