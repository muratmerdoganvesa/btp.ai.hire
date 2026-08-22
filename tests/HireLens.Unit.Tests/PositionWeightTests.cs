using FluentAssertions;
using Xunit;
using HireLens.Modules.Recruiting.Domain;

namespace HireLens.Unit.Tests;

public sealed class PositionWeightTests
{
    [Fact]
    public void Weights_must_sum_to_100()
    {
        var created = Position.Create(
            Guid.NewGuid(),
            "Backend",
            "Build APIs",
            [("C#", "Language", 40), ("SQL", "Data", 40)],
            DateTimeOffset.UtcNow);

        created.IsFailure.Should().BeTrue();
        created.Error.Message.Should().Contain("100");
    }

    [Fact]
    public void Valid_weights_are_accepted()
    {
        var created = Position.Create(
            Guid.NewGuid(),
            "Backend",
            "Build APIs",
            [("C#", "Language", 60), ("SQL", "Data", 40)],
            DateTimeOffset.UtcNow);

        created.IsSuccess.Should().BeTrue();
        created.Value.Criteria.Sum(c => c.Weight).Should().Be(100);
    }
}
