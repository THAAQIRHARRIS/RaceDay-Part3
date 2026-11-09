namespace RaceDay.Api.Auth;

/// <summary>The two roles supported by RaceDay.</summary>
public static class Roles
{
    public const string Organiser = "Organiser";
    public const string Participant = "Participant";

    public static readonly string[] All = { Organiser, Participant };
}

/// <summary>Keys used to store the logged-in user in the server-side session.</summary>
public static class SessionKeys
{
    public const string UserId = "UserId";
    public const string Role = "Role";
}
