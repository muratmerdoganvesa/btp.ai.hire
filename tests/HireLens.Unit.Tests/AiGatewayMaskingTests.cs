using FluentAssertions;
using Xunit;
using HireLens.AiGateway;
using HireLens.AiGateway.Masking;
using HireLens.AiGateway.Providers;
using HireLens.AiGateway.Routing;
using HireLens.Infrastructure.Persistence;
using HireLens.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HireLens.Unit.Tests;

public sealed class AiGatewayMaskingTests
{
    [Fact]
    public async Task Gateway_must_not_send_unmasked_pii_to_the_provider()
    {
        var spy = new SpyProvider();
        var tenant = new TenantContext();
        tenant.Resolve(Guid.NewGuid(), "tester", "masking-test");

        var options = Options.Create(new AiGatewayOptions
        {
            Profiles = new Dictionary<string, ModelProfile>
            {
                [nameof(AiTaskType.CvExtraction)] = new("stub-model", null, 128, 0)
            }
        });

        var db = CreateDb(tenant);
        var gateway = new HireLens.AiGateway.AiGateway(
            new PiiMasker(),
            spy,
            new ModelRouter(options),
            tenant,
            new SystemClock(),
            db);

        var input = "Candidate mail: person.alpha@example.com phone +1 202-555-0147 born 1990-01-15";

        await gateway.ExecuteAsync<StubPayload>(
            AiTaskType.CvExtraction,
            new PromptContext(input, "v0"),
            ct: CancellationToken.None);

        spy.LastPrompt.Should().NotBeNull();
        spy.LastPrompt!.Text.Should().NotContain("person.alpha@example.com");
        spy.LastPrompt.Text.Should().NotContain("202-555-0147");
        spy.LastPrompt.Text.Should().NotContain("1990-01-15");
        spy.LastPrompt.Text.Should().Contain("[EMAIL_");
        PiiMasker.ContainsUnmaskedPii(spy.LastPrompt.Text).Should().BeFalse();
    }

    [Fact]
    public async Task Provider_refuses_unmasked_email()
    {
        var provider = new StubAiProvider();
        var act = () => provider.CompleteAsync(
            new MaskedPrompt("write to person.alpha@example.com", new Dictionary<string, string>()),
            new ModelProfile("stub", null, 16, 0),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    private static HireLensDbContext CreateDb(ITenantContext tenant)
    {
        var options = new DbContextOptionsBuilder<HireLensDbContext>()
            .UseInMemoryDatabase($"masking-{Guid.NewGuid():N}")
            .Options;
        return new HireLensDbContext(options, tenant);
    }

    private sealed class SpyProvider : IAiProvider
    {
        public MaskedPrompt? LastPrompt { get; private set; }

        public Task<ProviderCompletion> CompleteAsync(
            MaskedPrompt prompt,
            ModelProfile profile,
            CancellationToken cancellationToken,
            OrchestrationPromptSpec? promptSpec = null)
        {
            _ = promptSpec;
            LastPrompt = prompt;
            return Task.FromResult(new ProviderCompletion("""{"status":"ok"}""", profile.ModelId, 1, 1, 0m));
        }
    }

    private sealed record StubPayload(string Status);
}
