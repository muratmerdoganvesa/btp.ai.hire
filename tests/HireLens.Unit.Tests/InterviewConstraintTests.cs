using FluentAssertions;
using Xunit;
using HireLens.Modules.Configuration.Application;
using HireLens.Modules.Interview.Domain;

namespace HireLens.Unit.Tests;

public sealed class InterviewConstraintTests
{
    [Fact]
    public void Question_without_criterion_is_rejected()
    {
        var session = InterviewSession.Invite(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "hash", DateTimeOffset.UtcNow);
        var added = session.AddQuestion(Guid.Empty, "Tell me about yourself.", 1);
        added.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Interview_cannot_start_without_disclosure()
    {
        var session = InterviewSession.Invite(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "hash", DateTimeOffset.UtcNow);
        session.Start().IsFailure.Should().BeTrue();
        session.AcceptDisclosure();
        session.Start().IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Pause_and_resume_persist_status()
    {
        var session = InterviewSession.Invite(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "hash", DateTimeOffset.UtcNow);
        session.AcceptDisclosure();
        session.Start();
        session.Pause().IsSuccess.Should().BeTrue();
        session.Status.Should().Be("paused");
        session.Resume().IsSuccess.Should().BeTrue();
        session.Status.Should().Be("in_progress");
    }

    [Fact]
    public void Brand_hue_contrast_is_evaluated()
    {
        Contrast.IsAa(250).Should().BeTrue();
    }
}
