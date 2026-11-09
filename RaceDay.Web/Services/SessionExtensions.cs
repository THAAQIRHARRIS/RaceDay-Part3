namespace RaceDay.Web.Services;

/// <summary>Keys used in the MVC application's server-side session.</summary>
public static class SessionKeys
{
    public const string UserId = "UserId";
    public const string Role = "Role";
    public const string FullName = "FullName";
    public const string ProfileImageUrl = "ProfileImageUrl";
    public const string ApiCookie = "ApiCookie";   // the API's session id, forwarded on every API call
}

public static class Roles
{
    public const string Organiser = "Organiser";
    public const string Participant = "Participant";
}

public static class SessionExtensions
{
    public static bool IsLoggedIn(this ISession session) =>
        session.GetInt32(SessionKeys.UserId) != null && !string.IsNullOrEmpty(session.GetString(SessionKeys.Role));

    public static string? GetRole(this ISession session) => session.GetString(SessionKeys.Role);

    public static int CurrentUserId(this ISession session) => session.GetInt32(SessionKeys.UserId) ?? 0;
}
