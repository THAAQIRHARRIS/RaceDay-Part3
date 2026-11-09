using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Filters;
using RaceDay.Web.Models;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

public class AccountController : Controller
{
    private readonly ApiClient _api;

    public AccountController(ApiClient api) => _api = api;

    // ------------------------------------------------------------------ login

    [HttpGet]
    public IActionResult Login(string? returnUrl = null, bool expired = false)
    {
        if (HttpContext.Session.IsLoggedIn()) return RedirectToRole();

        if (expired) ViewBag.Notice = "Your session has expired. Please log in again.";
        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        // Start from a clean session so a previous user's data (or API cookie) can never leak into this login.
        HttpContext.Session.Clear();

        var result = await _api.SendAsync<UserDto>(
            HttpMethod.Post, "api/auth/login",
            JsonContent.Create(new { email = model.Email, password = model.Password }),
            expireSessionOn401: false);

        if (!result.Success || result.Data == null)
        {
            ModelState.AddModelError(string.Empty,
                result.Status == HttpStatusCode.Unauthorized ? "Invalid email or password." : result.Error);
            return View(model);
        }

        var user = result.Data;
        HttpContext.Session.SetInt32(SessionKeys.UserId, user.UserId);
        HttpContext.Session.SetString(SessionKeys.Role, user.Role);
        HttpContext.Session.SetString(SessionKeys.FullName, $"{user.FirstName} {user.LastName}");
        HttpContext.Session.SetString(SessionKeys.ProfileImageUrl, user.ProfileImageUrl ?? string.Empty);

        if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
            return LocalRedirect(model.ReturnUrl);

        return RedirectToRole();
    }

    // --------------------------------------------------------------- register

    [HttpGet]
    public IActionResult Register(string? role = null)
    {
        if (HttpContext.Session.IsLoggedIn()) return RedirectToRole();

        var model = new RegisterViewModel();
        if (string.Equals(role, Roles.Organiser, StringComparison.OrdinalIgnoreCase))
            model.Role = Roles.Organiser;
        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (model.Role != Roles.Organiser && model.Role != Roles.Participant)
            ModelState.AddModelError(nameof(model.Role), "Please choose Participant or Organiser.");

        if (model.Role == Roles.Organiser && string.IsNullOrWhiteSpace(model.OrganiserCode))
            ModelState.AddModelError(nameof(model.OrganiserCode), "Organisers need an organiser code.");

        if (!ModelState.IsValid) return View(model);

        var result = await _api.PostAsync<UserDto>("api/auth/register", new
        {
            firstName = model.FirstName,
            lastName = model.LastName,
            email = model.Email,
            password = model.Password,
            role = model.Role,
            phoneNumber = string.IsNullOrWhiteSpace(model.PhoneNumber) ? null : model.PhoneNumber,
            organiserCode = model.Role == Roles.Organiser ? model.OrganiserCode : null
        });

        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.Error);
            return View(model);
        }

        TempData["Success"] = "Your account has been created. Please log in.";
        return RedirectToAction(nameof(Login));
    }

    // ----------------------------------------------------------------- logout

    [HttpPost]
    public async Task<IActionResult> Logout()
    {
        try
        {
            await _api.PostAsync<EmptyResponse>("api/auth/logout");
        }
        catch (Exception ex) when (ex is SessionExpiredException or ApiUnavailableException)
        {
            // Logging out must always work locally, even if the API cannot be reached.
        }

        HttpContext.Session.Clear();
        TempData["Success"] = "You have been logged out.";
        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    public IActionResult AccessDenied() => View();

    // ---------------------------------------------------------------- profile

    [HttpGet, RoleAuthorize]
    public async Task<IActionResult> Profile()
    {
        var model = await LoadProfileAsync();
        return model == null ? RedirectToAction("Index", "Home") : View(model);
    }

    [HttpPost, RoleAuthorize]
    public async Task<IActionResult> Profile(ProfileViewModel model)
    {
        if (!ModelState.IsValid)
        {
            await FillReadOnlyFieldsAsync(model);
            return View(model);
        }

        var result = await _api.PutAsync<UserDto>("api/users/me", new
        {
            firstName = model.FirstName,
            lastName = model.LastName,
            phoneNumber = string.IsNullOrWhiteSpace(model.PhoneNumber) ? null : model.PhoneNumber
        });

        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.Error);
            await FillReadOnlyFieldsAsync(model);
            return View(model);
        }

        HttpContext.Session.SetString(SessionKeys.FullName, $"{model.FirstName} {model.LastName}");
        TempData["Success"] = "Your profile has been updated.";
        return RedirectToAction(nameof(Profile));
    }

    [HttpPost, RoleAuthorize]
    public async Task<IActionResult> ProfileImage(IFormFile? file)
    {
        if (file == null || file.Length == 0)
        {
            TempData["Error"] = "Please choose an image to upload.";
            return RedirectToAction(nameof(Profile));
        }

        var result = await _api.UploadImageAsync("api/users/me/profile-image", file);
        if (result.Success && result.Data != null)
        {
            HttpContext.Session.SetString(SessionKeys.ProfileImageUrl, result.Data.ImageUrl);
            TempData["Success"] = "Your profile picture has been updated.";
        }
        else
        {
            TempData["Error"] = result.Error;
        }
        return RedirectToAction(nameof(Profile));
    }

    // ---------------------------------------------------------------- helpers

    private IActionResult RedirectToRole() =>
        HttpContext.Session.GetRole() == Roles.Organiser
            ? RedirectToAction("Dashboard", "Organiser")
            : RedirectToAction("Index", "Home");

    private async Task<ProfileViewModel?> LoadProfileAsync()
    {
        var result = await _api.GetAsync<UserDto>("api/users/me");
        if (!result.Success || result.Data == null)
        {
            TempData["Error"] = result.Error;
            return null;
        }

        var user = result.Data;
        return new ProfileViewModel
        {
            FirstName = user.FirstName,
            LastName = user.LastName,
            PhoneNumber = user.PhoneNumber,
            Email = user.Email,
            Role = user.Role,
            ProfileImageUrl = user.ProfileImageUrl
        };
    }

    private async Task FillReadOnlyFieldsAsync(ProfileViewModel model)
    {
        var current = await LoadProfileAsync();
        if (current == null) return;
        model.Email = current.Email;
        model.Role = current.Role;
        model.ProfileImageUrl = current.ProfileImageUrl;
    }
}
