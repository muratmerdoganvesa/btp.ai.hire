using System.Threading.Channels;

namespace HireLens.Api.Jobs;

public enum AnalysisWorkKind
{
    Parse,
    Match,
    Evaluation
}

public sealed record AnalysisWork(
    Guid TenantId,
    AnalysisWorkKind Kind,
    Guid DocumentId,
    Guid JobId);

/// <summary>
/// In-process queue so parse/match do not block HTTP. Same API process as
/// <see cref="AnalysisJobWorker"/> — CF does not have a separate Hangfire store.
/// </summary>
public sealed class AnalysisJobQueue
{
    private readonly Channel<AnalysisWork> _channel = Channel.CreateUnbounded<AnalysisWork>(
        new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

    public void Enqueue(AnalysisWork work)
    {
        if (!_channel.Writer.TryWrite(work))
        {
            throw new InvalidOperationException("Analysis job queue is closed.");
        }
    }

    public IAsyncEnumerable<AnalysisWork> ReadAllAsync(CancellationToken cancellationToken) =>
        _channel.Reader.ReadAllAsync(cancellationToken);
}
