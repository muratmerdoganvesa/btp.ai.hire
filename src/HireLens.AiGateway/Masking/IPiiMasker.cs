using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace HireLens.AiGateway.Masking;

public sealed record MaskedPrompt(string Text, IReadOnlyDictionary<string, string> Mapping);

public interface IPiiMasker
{
    MaskedPrompt Mask(string input);
}

/// <summary>
/// Reversible pseudonymization. Mapping stays in process memory and is never
/// sent to a model. Patterns cover contact and demographic fields that must
/// not enter an employment-assessment prompt.
/// </summary>
public sealed partial class PiiMasker : IPiiMasker
{
    public MaskedPrompt Mask(string input)
    {
        var mapping = new Dictionary<string, string>(StringComparer.Ordinal);
        var text = input;

        text = Replace(text, Email(), "EMAIL", mapping);
        text = Replace(text, Phone(), "PHONE", mapping);
        text = Replace(text, IsoDate(), "DATE", mapping);
        text = Replace(text, NationalIdLike(), "ID", mapping);

        if (ContainsUnmaskedPii(text))
        {
            throw new InvalidOperationException("PII remained after masking; refusing to leave the gateway.");
        }

        return new MaskedPrompt(text, mapping);
    }

    public static bool ContainsUnmaskedPii(string text) =>
        Email().IsMatch(text) || Phone().IsMatch(text);

    private static string Replace(string text, Regex regex, string kind, IDictionary<string, string> mapping)
    {
        return regex.Replace(text, match =>
        {
            var token = $"[{kind}_{ShortHash(match.Value)}]";
            mapping[token] = match.Value;
            return token;
        });
    }

    private static string ShortHash(string value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(hash)[..8];
    }

    [GeneratedRegex(@"[A-Z0-9._%+\-]+@[A-Z0-9.\-]+\.[A-Z]{2,}", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Email();

    [GeneratedRegex(@"(?:\+|00)?\d{1,3}[\s\-\.]?\(?\d{2,4}\)?[\s\-\.]?\d{3,4}[\s\-\.]?\d{3,4}")]
    private static partial Regex Phone();

    [GeneratedRegex(@"\b(?:19|20)\d{2}[-/.](?:0[1-9]|1[0-2])[-/.](?:0[1-9]|[12]\d|3[01])\b")]
    private static partial Regex IsoDate();

    [GeneratedRegex(@"\b[A-Z]{1,2}\d{6,12}\b")]
    private static partial Regex NationalIdLike();
}
