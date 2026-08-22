using FluentAssertions;
using Xunit;
using HireLens.Modules.Review.Domain;

namespace HireLens.Unit.Tests;

public sealed class DecisionRationaleTests
{
    [Fact]
    public void Decision_without_rationale_is_rejected()
    {
        var created = Decision.Record(Guid.NewGuid(), Guid.NewGuid(), "advance", "  ", DateTimeOffset.UtcNow);

        created.IsFailure.Should().BeTrue();
        created.Error.Message.Should().Contain("rationale");
    }

    [Fact]
    public void Decision_with_rationale_is_accepted()
    {
        var created = Decision.Record(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "hold",
            "Need a live coding sample for the C# criterion.",
            DateTimeOffset.UtcNow);

        created.IsSuccess.Should().BeTrue();
        created.Value.Outcome.Should().Be("hold");
    }
}
