namespace HireLens.Infrastructure.Persistence;

public sealed class CompositeAuditWriter(IEnumerable<IAuditSink> sinks) : IAuditWriter
{
    public async Task WriteAsync(IReadOnlyList<AuditEvent> events, CancellationToken cancellationToken = default)
    {
        foreach (var sink in sinks)
        {
            await sink.WriteAsync(events, cancellationToken);
        }
    }
}

public interface IAuditSink
{
    Task WriteAsync(IReadOnlyList<AuditEvent> events, CancellationToken cancellationToken = default);
}
