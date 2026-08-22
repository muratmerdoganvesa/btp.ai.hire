namespace HireLens.Infrastructure.Persistence;

public interface IAuditWriter
{
    Task WriteAsync(IReadOnlyList<AuditEvent> events, CancellationToken cancellationToken = default);
}
