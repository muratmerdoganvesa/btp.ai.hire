using FluentAssertions;
using HireLens.AiGateway;
using HireLens.AiGateway.Providers;
using HireLens.Contracts.Interview;
using HireLens.Contracts.Recruiting;
using HireLens.Modules.Interview.Application;
using HireLens.SharedKernel;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using System.Text.Json;
using Xunit;

namespace HireLens.Unit.Tests;

public sealed class InterviewEvaluationServiceTests
{
    [Fact]
    public async Task Calls_existing_interview_evaluation_task_with_placeholders_only()
    {
        PromptContext? captured = null;
        var gateway = Substitute.For<IAiGateway>();
        gateway.ExecuteAsync<string>(
                Arg.Any<AiTaskType>(),
                Arg.Any<PromptContext>(),
                Arg.Any<AiOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                captured = ci.Arg<PromptContext>();
                return Task.FromResult(new AiResult<string>(
                    """{"rubricId":"r1","criteria":[{"criterionId":"csharp","score":70}],"warnings":[]}""",
                    "anthropic--claude-4.5-haiku",
                    "1",
                    1,
                    1,
                    0m,
                    TimeSpan.FromMilliseconds(1),
                    null,
                    []));
            });

        var svc = new InterviewEvaluationService(
            gateway,
            Options.Create(new SapAiCoreOptions { DeploymentId = "d08b1ad950db57c6" }),
            NullLogger<InterviewEvaluationService>.Instance);

        var result = await svc.EvaluateAsync(
            new EvaluateInterviewRequest(
                JsonSerializer.Deserialize<JsonElement>(
                    """{"rubricId":"r1","criteria":[{"criterionId":"csharp","name":"C#","weight":100}]}"""),
                [new ExtractedInterviewQuestionDto("q1", "csharp", "Anlatın.", ["örnek"])],
                "[00:03:20] Aday: C# API yazdım.",
                default,
                "Backend"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await gateway.Received(1).ExecuteAsync<string>(
            AiTaskType.InterviewEvaluation,
            Arg.Any<PromptContext>(),
            Arg.Any<AiOptions?>(),
            Arg.Any<CancellationToken>());

        captured.Should().NotBeNull();
        captured!.PlaceholdersOnly.Should().BeTrue();
        captured.SystemPrompt.Should().BeNull();
        captured.UserPrompt.Should().BeNull();
        captured.DeploymentId.Should().Be("d08b1ad950db57c6");
        captured.Variables.Should().ContainKey("transcript");
        captured.Variables!["transcript"].Should().Be("[00:03:20] Aday: C# API yazdım.");
        captured.Variables["cv_match_result"].Should().BeEmpty();
        captured.Variables["job_title"].Should().Be("Backend");
        captured.Variables["rubric"].Should().NotContain("[object Object]");
    }

    [Fact]
    public async Task Does_not_call_gateway_when_criteria_empty()
    {
        var gateway = Substitute.For<IAiGateway>();
        var svc = new InterviewEvaluationService(
            gateway,
            Options.Create(new SapAiCoreOptions { DeploymentId = "d08b1ad950db57c6" }),
            NullLogger<InterviewEvaluationService>.Instance);

        var result = await svc.EvaluateAsync(
            new EvaluateInterviewRequest(
                JsonSerializer.Deserialize<JsonElement>("""{"rubricId":"r1","criteria":[]}"""),
                [new ExtractedInterviewQuestionDto("q1", "csharp", "Anlatın.", [])],
                "metin"),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        await gateway.DidNotReceive().ExecuteAsync<string>(
            Arg.Any<AiTaskType>(),
            Arg.Any<PromptContext>(),
            Arg.Any<AiOptions?>(),
            Arg.Any<CancellationToken>());
    }
}
