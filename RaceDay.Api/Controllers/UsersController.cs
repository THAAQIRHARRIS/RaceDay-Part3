using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Auth;
using RaceDay.Api.Data;
using RaceDay.Api.DTOs;

namespace RaceDay.Api.Controllers;

[Route("api/users")]
[SessionAuthorize]
public class UsersController : ApiControllerBase
{
    private readonly RaceDayDbContext _db;
    public UsersController(RaceDayDbContext db) => _db = db;

    /// <summary>Get the logged-in user's own profile.</summary>
    /// <remarks>Role required: any logged-in user (Organiser or Participant).</remarks>
    [HttpGet("me")]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UserResponse>> GetMe()
    {
        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == CurrentUserId);
        return user == null ? Fail(StatusCodes.Status404NotFound, "User not found.") : Ok(UserResponse.From(user));
    }

    /// <summary>Update the logged-in user's own profile (first name, last name, phone number).</summary>
    /// <remarks>Role required: any logged-in user. Email and role cannot be changed here.</remarks>
    [HttpPut("me")]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UserResponse>> UpdateMe(UpdateProfileRequest request)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.UserId == CurrentUserId);
        if (user == null) return Fail(StatusCodes.Status404NotFound, "User not found.");

        user.FirstName = request.FirstName.Trim();
        user.LastName = request.LastName.Trim();
        user.PhoneNumber = request.PhoneNumber;
        await _db.SaveChangesAsync();

        return Ok(UserResponse.From(user));
    }
}
