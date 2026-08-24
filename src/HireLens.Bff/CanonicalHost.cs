namespace HireLens.Bff;

public static class CanonicalHost
{
    public const string Default = "hirelens-web.cfapps.eu20-002.hana.ondemand.com";

    private static readonly System.Text.RegularExpressions.Regex TenantAppHost = new(
        @"^[a-z0-9-]+-hirelens-web\.cfapps\.eu20-002\.hana\.ondemand\.com$",
        System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Compiled);

    public static bool IsAlias(string? host, string canonical)
    {
        var hostname = (host ?? string.Empty).Split(':')[0];
        return TenantAppHost.IsMatch(hostname) &&
               !hostname.Equals(canonical, StringComparison.OrdinalIgnoreCase);
    }

    public static string Origin(string canonical) => $"https://{canonical}";
}
