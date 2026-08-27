using System.Text;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace HireLens.Modules.Documents.Application;

public enum ExtractionStatus
{
    Ok,
    Unusable
}

public sealed record ExtractionResult(ExtractionStatus Status, string Text, string? Reason = null)
{
    public static ExtractionResult Ok(string text) => new(ExtractionStatus.Ok, text);

    public static ExtractionResult Unusable(string reason) =>
        new(ExtractionStatus.Unusable, string.Empty, reason);
}

/// <summary>
/// PDF / DOCX / TXT → plain text. Scanned (image-only) PDFs yield Unusable;
/// OCR is out of scope for this release.
/// </summary>
public static partial class CvTextExtractor
{
    public const int MinimumUsableChars = 200;

    public static ExtractionResult Extract(string fileName, string contentType, byte[] bytes)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        string raw;

        try
        {
            if (contentType is "text/plain" || ext == ".txt")
            {
                raw = Encoding.UTF8.GetString(bytes);
            }
            else if (contentType is "application/pdf" || ext == ".pdf")
            {
                raw = ExtractPdf(bytes);
            }
            else if (contentType is
                         "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
                     || ext == ".docx")
            {
                raw = ExtractDocx(bytes);
            }
            else
            {
                return ExtractionResult.Unusable("Unsupported document type.");
            }
        }
        catch (Exception ex)
        {
            return ExtractionResult.Unusable($"Document could not be read: {ex.Message}");
        }

        var normalized = Normalize(raw);
        if (normalized.Length < MinimumUsableChars)
        {
            return ExtractionResult.Unusable(
                "Document appears scanned or empty; text could not be extracted.");
        }

        return ExtractionResult.Ok(normalized);
    }

    public static string Normalize(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var cleaned = ControlChars().Replace(text, string.Empty);
        cleaned = cleaned.Replace("\r\n", "\n").Replace('\r', '\n');
        cleaned = MultiSpace().Replace(cleaned, " ");
        cleaned = MultiBlankLines().Replace(cleaned, "\n\n");
        return cleaned.Trim();
    }

    private static string ExtractPdf(byte[] bytes)
    {
        using var document = PdfDocument.Open(bytes);
        var sb = new StringBuilder();
        foreach (var page in document.GetPages())
        {
            var ordered = ExtractPageReadingOrder(page);
            sb.AppendLine(string.IsNullOrWhiteSpace(ordered) ? page.Text : ordered);
        }

        return sb.ToString();
    }

    /// <summary>
    /// Designer CVs paint glyphs in drawing order (header color, icons, columns),
    /// not reading order. <see cref="Page.Text"/> then looks garbled to the LLM.
    /// </summary>
    private static string ExtractPageReadingOrder(Page page)
    {
        var words = page.GetWords().ToList();
        if (words.Count == 0)
        {
            return page.Text ?? string.Empty;
        }

        var lineTolerance = Math.Max(2.0, page.Height * 0.01);
        var lines = new List<List<Word>>();
        foreach (var word in words
                     .OrderByDescending(w => w.BoundingBox.Centroid.Y)
                     .ThenBy(w => w.BoundingBox.Left))
        {
            var y = word.BoundingBox.Centroid.Y;
            var current = lines.Count == 0 ? null : lines[^1];
            if (current is null || Math.Abs(current[0].BoundingBox.Centroid.Y - y) > lineTolerance)
            {
                current = [];
                lines.Add(current);
            }

            current.Add(word);
        }

        return string.Join(
            '\n',
            lines.Select(line => string.Join(
                ' ',
                line.OrderBy(w => w.BoundingBox.Left).Select(w => w.Text))));
    }

    private static string ExtractDocx(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        using var word = WordprocessingDocument.Open(stream, false);
        var body = word.MainDocumentPart?.Document.Body;
        if (body is null)
        {
            return string.Empty;
        }

        var sb = new StringBuilder();
        foreach (var paragraph in body.Elements<Paragraph>())
        {
            sb.AppendLine(paragraph.InnerText);
        }

        return sb.ToString();
    }

    [GeneratedRegex(@"[\u0000-\u0008\u000B\u000C\u000E-\u001F]")]
    private static partial Regex ControlChars();

    [GeneratedRegex(@"[^\S\n]{2,}")]
    private static partial Regex MultiSpace();

    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex MultiBlankLines();
}
