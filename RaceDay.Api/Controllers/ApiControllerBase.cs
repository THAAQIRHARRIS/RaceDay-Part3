using Microsoft.AspNetCore.Mvc;
using RaceDay.Api.Auth;

namespace RaceDay.Api.Controllers;

[ApiController]
[Produces("application/json")]
public abstract class ApiControllerBase : ControllerBase
{
    /// <summary>Id of the logged-in user, read from the server-side session (0 when not logged in).</summary>
    protected int CurrentUserId => HttpContext.Session.GetInt32(SessionKeys.UserId) ?? 0;

    protected ObjectResult Fail(int statusCode, string detail) => Problem(detail: detail, statusCode: statusCode);
}
