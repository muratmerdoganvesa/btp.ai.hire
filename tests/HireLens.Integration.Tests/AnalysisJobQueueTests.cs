using FluentAssertions;
using HireLens.Api.Jobs;
using Xunit;

namespace HireLens.Integration.Tests;

public sealed class AnalysisJobQueueTests
{
    [Fact]
    public async Task Enqueue_then_read_preserves_order_without_blocking()
    {
        var queue = new AnalysisJobQueue();
        var first = new AnalysisWork(Guid.NewGuid(), AnalysisWorkKind.Parse, Guid.NewGuid(), Guid.NewGuid());
        var second = new AnalysisWork(first.TenantId, AnalysisWorkKind.Match, first.DocumentId, first.DocumentId);

        queue.Enqueue(first);
        queue.Enqueue(second);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var seen = new List<AnalysisWorkKind>();
        await foreach (var work in queue.ReadAllAsync(cts.Token))
        {
            seen.Add(work.Kind);
            if (seen.Count == 2)
            {
                break;
            }
        }

        seen.Should().Equal(AnalysisWorkKind.Parse, AnalysisWorkKind.Match);
    }
}
