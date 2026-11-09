using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using RaceDay.Web.Services;

namespace RaceDay.Web.Filters;

/// <summary>Turns "API down" and "session expired" into friendly redirects instead of error pages.</summary>
public class ApiExceptionFilter : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        switch (context.Exception)
        {
            case SessionExpiredException:
                context.HttpContext.Session.Clear();
                context.Result = new RedirectToActionResult("Login", "Account", new { expired = true });
                context.ExceptionHandled = true;
                break;

            case ApiUnavailableException:
                context.Result = new RedirectToActionResult("Unavailable", "Home", null);
                context.ExceptionHandled = true;
                break;
        }
    }
}
