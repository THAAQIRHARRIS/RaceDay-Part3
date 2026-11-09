using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace RaceDay.Api.Auth;

/// <summary>
/// Protects an endpoint using the server-side session created at login.
/// No roles listed = any logged-in user. Roles listed = only those roles.
/// Returns 401 when there is no session and 403 when the role is not allowed.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public class SessionAuthorizeAttribute : Attribute, IAuthorizationFilter
{
    private readonly string[] _roles;

    public SessionAuthorizeAttribute(params string[] roles) => _roles = roles;

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var session = context.HttpContext.Session;
        var userId = session.GetInt32(SessionKeys.UserId);
        var role = session.GetString(SessionKeys.Role);

        if (userId == null || string.IsNullOrEmpty(role))
        {
            context.Result = new ObjectResult(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Unauthorized",
                Detail = "You must be logged in to access this resource."
            })
            { StatusCode = StatusCodes.Status401Unauthorized };
            return;
        }

        if (_roles.Length > 0 && !_roles.Contains(role))
        {
            context.Result = new ObjectResult(new ProblemDetails
            {
                Status = StatusCodes.Status403Forbidden,
                Title = "Forbidden",
                Detail = $"This action requires the role: {string.Join(" or ", _roles)}."
            })
            { StatusCode = StatusCodes.Status403Forbidden };
        }
    }
}
