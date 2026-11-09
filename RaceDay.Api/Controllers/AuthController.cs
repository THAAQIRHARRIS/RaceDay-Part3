using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Auth;
using RaceDay.Api.Data;
using RaceDay.Api.DTOs;
using RaceDay.Api.Models;

namespace RaceDay.Api.Controllers;

[Route("api/auth")]
public class AuthController : ApiControllerBase
{
    private readonly RaceDayDbContext _db;
    private readonly IConfiguration _config;

    public AuthController(RaceDayDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    /// <summary>Register a new account as an Organiser or a Participant.</summary>
    /// <remarks>
    /// Role required: none (public). Passwords are hashed with BCrypt before storage.
    /// To stop anyone self-promoting to Organiser, registering as an Organiser also requires
    /// the secret <c>organiserCode</c> configured on the server.
    /// </remarks>
    [HttpPost("register")]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UserResponse>> Register(RegisterRequest request)
    {
        var role = Roles.All.FirstOrDefault(r => string.Equals(r, request.Role, StringComparison.OrdinalIgnoreCase));
        if (role == null)
            return Fail(StatusCodes.Status400BadRequest, "Role must be either 'Organiser' or 'Participant'.");

        if (role == Roles.Organiser)
        {
            var expected = _config["Registration:OrganiserCode"];
            if (string.IsNullOrEmpty(expected) || request.OrganiserCode != expected)
                return Fail(StatusCodes.Status403Forbidden, "A valid organiser code is required to register as an Organiser.");
        }

        var email = request.Email.Trim().ToLowerInvariant();
        if (await _db.Users.AnyAsync(u => u.Email == email))
            return Fail(StatusCodes.Status409Conflict, "An account with this email already exists.");

        var user = new User
        {
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Role = role,
            PhoneNumber = request.PhoneNumber
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        return Created("/api/users/me", UserResponse.From(user));
    }

    /// <summary>Log in and start a session that stores the user's id and role.</summary>
    /// <remarks>Role required: none (public). After a successful login the session cookie is sent automatically on later requests.</remarks>
    [HttpPost("login")]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UserResponse>> Login(LoginRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);

        if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            return Fail(StatusCodes.Status401Unauthorized, "Invalid email or password.");

        HttpContext.Session.Clear();
        HttpContext.Session.SetInt32(SessionKeys.UserId, user.UserId);
        HttpContext.Session.SetString(SessionKeys.Role, user.Role);

        return Ok(UserResponse.From(user));
    }

    /// <summary>Log out and clear the session.</summary>
    /// <remarks>Role required: any logged-in user.</remarks>
    [HttpPost("logout")]
    [SessionAuthorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public IActionResult Logout()
    {
        HttpContext.Session.Clear();
        return Ok(new { message = "Logged out." });
    }
}
