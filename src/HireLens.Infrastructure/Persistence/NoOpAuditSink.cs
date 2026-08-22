namespace HireLens.Infrastructure.Persistence;

/// <summary>
/// Local table rows are the source of truth. This sink exists so IAuditWriter
/// always has at least one registered implementation when SAP Audit Log is unbound.
/// </summary>
public sealed class NoOpAuditSink : IAuditSink
{
    public Task WriteAsync(IReadOnlyList<AuditEvent> events, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
