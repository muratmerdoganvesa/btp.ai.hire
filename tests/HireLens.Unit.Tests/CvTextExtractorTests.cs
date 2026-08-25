using System.Text;
using FluentAssertions;
using HireLens.Modules.Documents.Application;
using Xunit;

namespace HireLens.Unit.Tests;

public sealed class CvTextExtractorTests
{
    [Fact]
    public void Short_text_is_unusable()
    {
        var bytes = Encoding.UTF8.GetBytes("too short");
        var result = CvTextExtractor.Extract("cv.txt", "text/plain", bytes);

        result.Status.Should().Be(ExtractionStatus.Unusable);
        result.Reason.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Long_plain_text_is_ok_and_normalized()
    {
        var body = string.Join(' ', Enumerable.Repeat("experience with SAP SuccessFactors consulting", 20));
        var bytes = Encoding.UTF8.GetBytes("Title\n\n\n" + body + "   extra");
        var result = CvTextExtractor.Extract("cv.txt", "text/plain", bytes);

        result.Status.Should().Be(ExtractionStatus.Ok);
        result.Text.Length.Should().BeGreaterThanOrEqualTo(CvTextExtractor.MinimumUsableChars);
        result.Text.Should().NotContain("\n\n\n");
    }

    [Fact]
    public void Unsupported_extension_is_unusable()
    {
        var result = CvTextExtractor.Extract("cv.png", "image/png", [1, 2, 3]);
        result.Status.Should().Be(ExtractionStatus.Unusable);
    }
}
