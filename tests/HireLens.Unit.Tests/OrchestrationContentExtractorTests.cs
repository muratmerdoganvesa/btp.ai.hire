using FluentAssertions;
using HireLens.AiGateway.Providers;
using Xunit;

namespace HireLens.Unit.Tests;

public sealed class OrchestrationContentExtractorTests
{
    [Fact]
    public void Reads_prompt_templating_module_when_orchestration_result_is_absent()
    {
        const string raw = """
            {
              "module_results": {
                "prompt_templating": {
                  "choices": [
                    {
                      "message": {
                        "role": "assistant",
                        "content": "{\"rubric\":{\"criteria\":[{\"name\":\"X\",\"weight\":100}]}}"
                      }
                    }
                  ]
                }
              }
            }
            """;

        var (content, _, _) = OrchestrationContentExtractor.Extract(raw);

        content.Should().Contain("\"name\":\"X\"");
        content.Should().NotContain("module_results");
    }

    [Fact]
    public void Reads_claude_text_part_array()
    {
        const string raw = """
            {
              "orchestration_result": {
                "choices": [
                  {
                    "message": {
                      "content": [
                        { "type": "text", "text": "{\"criteria\":[]}" }
                      ]
                    }
                  }
                ]
              }
            }
            """;

        var (content, _, _) = OrchestrationContentExtractor.Extract(raw);

        content.Should().Be("{\"criteria\":[]}");
    }

    [Fact]
    public void Reads_already_parsed_json_object_content()
    {
        const string raw = """
            {
              "orchestration_result": {
                "choices": [
                  {
                    "message": {
                      "content": { "rubric": { "criteria": [{ "name": "Hibe", "weight": 100 }] } }
                    }
                  }
                ]
              }
            }
            """;

        var (content, _, _) = OrchestrationContentExtractor.Extract(raw);

        content.Should().Contain("Hibe");
        content.Should().Contain("rubric");
    }
}
