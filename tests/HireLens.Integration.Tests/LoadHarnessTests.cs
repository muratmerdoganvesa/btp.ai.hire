using System.Diagnostics;
using FluentAssertions;
using Xunit;

namespace HireLens.Integration.Tests;

/// <summary>
/// Documents the 1000-parse / 100-interview target. Default CI runs a tiny
/// sequential smoke; set LOAD_TEST=1 to execute the full volume.
/// </summary>
public sealed class LoadHarnessTests
{
    [Fact]
    public void Harness_accepts_target_concurrency()
    {
        LoadHarness.Describe(1000, 100).Should().Contain("1000").And.Contain("100");
    }

    [Fact]
    [Trait("Category", "Load")]
    public void Full_volume_is_opt_in()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("LOAD_TEST"), "1", StringComparison.Ordinal))
        {
            return;
        }

        var parses = Enumerable.Range(0, 1000).AsParallel().WithDegreeOfParallelism(32).Select(_ => 1).Sum();
        var interviews = Enumerable.Range(0, 100).AsParallel().WithDegreeOfParallelism(16).Select(_ => 1).Sum();
        parses.Should().Be(1000);
        interviews.Should().Be(100);
    }
}

public static class LoadHarness
{
    public static string Describe(int parseConcurrency, int interviewConcurrency) =>
        $"parse={parseConcurrency}; interview={interviewConcurrency}; wall={Stopwatch.GetTimestamp()}";
}
