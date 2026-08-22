namespace HireLens.Contracts;

public static class Roles
{
    public const string Recruiter = "Recruiter";
    public const string HiringManager = "HiringManager";
    public const string TenantAdmin = "TenantAdmin";

    public static readonly IReadOnlyList<string> All = [Recruiter, HiringManager, TenantAdmin];
}
