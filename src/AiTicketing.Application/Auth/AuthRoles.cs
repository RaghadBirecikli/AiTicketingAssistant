namespace AiTicketing.Application.Auth;

public static class AuthRoles
{
    public const string Admin = "Admin";
    public const string Agent = "Agent";
    public const string Customer = "Customer";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(
        [Admin, Agent, Customer],
        StringComparer.OrdinalIgnoreCase);

    public static string Normalize(string role) =>
        All.First(allowedRole => string.Equals(allowedRole, role, StringComparison.OrdinalIgnoreCase));
}
