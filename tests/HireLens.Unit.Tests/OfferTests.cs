using FluentAssertions;
using Xunit;
using HireLens.Modules.Review.Domain;

namespace HireLens.Unit.Tests;

public sealed class OfferTests
{
    [Fact]
    public void Draft_without_package_is_rejected()
    {
        var created = Offer.Draft(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "  ",
            null,
            59,
            DateTimeOffset.UtcNow);

        created.IsFailure.Should().BeTrue();
        created.Error.Message.Should().Contain("Package");
    }

    [Fact]
    public void Draft_can_be_sent_then_accepted()
    {
        var now = DateTimeOffset.UtcNow;
        var created = Offer.Draft(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Brüt 85.000 TL, yıllık izin 20 gün.",
            "İK notu",
            72,
            now);

        created.IsSuccess.Should().BeTrue();
        created.Value.Status.Should().Be("draft");
        created.Value.Send(now.AddMinutes(1)).IsSuccess.Should().BeTrue();
        created.Value.Status.Should().Be("sent");
        created.Value.Accept(now.AddMinutes(2)).IsSuccess.Should().BeTrue();
        created.Value.Status.Should().Be("accepted");
    }

    [Fact]
    public void Sent_offer_cannot_be_edited()
    {
        var now = DateTimeOffset.UtcNow;
        var created = Offer.Draft(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Paket: 80.000 TL",
            null,
            null,
            now).Value;
        created.Send(now).IsSuccess.Should().BeTrue();

        var updated = created.UpdateDraft("Paket: 90.000 TL", null, now.AddMinutes(1));

        updated.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Draft_cannot_be_accepted()
    {
        var created = Offer.Draft(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Paket: 80.000 TL",
            null,
            null,
            DateTimeOffset.UtcNow).Value;

        created.Accept(DateTimeOffset.UtcNow).IsFailure.Should().BeTrue();
    }
}
