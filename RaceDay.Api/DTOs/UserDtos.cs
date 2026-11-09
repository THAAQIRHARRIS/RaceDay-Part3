using System.ComponentModel.DataAnnotations;
using RaceDay.Api.Models;

namespace RaceDay.Api.DTOs;

public class RegisterRequest
{
    [Required, StringLength(100)] public string FirstName { get; set; } = string.Empty;
    [Required, StringLength(100)] public string LastName { get; set; } = string.Empty;
    [Required, EmailAddress, StringLength(150)] public string Email { get; set; } = string.Empty;
    [Required, StringLength(100, MinimumLength = 8)] public string Password { get; set; } = string.Empty;

    /// <summary>"Organiser" or "Participant".</summary>
    [Required] public string Role { get; set; } = string.Empty;

    [Phone, StringLength(20)] public string? PhoneNumber { get; set; }

    /// <summary>Secret invitation code. Required only when registering as an Organiser.</summary>
    public string? OrganiserCode { get; set; }
}

public class LoginRequest
{
    [Required, EmailAddress] public string Email { get; set; } = string.Empty;
    [Required] public string Password { get; set; } = string.Empty;
}

public class UpdateProfileRequest
{
    [Required, StringLength(100)] public string FirstName { get; set; } = string.Empty;
    [Required, StringLength(100)] public string LastName { get; set; } = string.Empty;
    [Phone, StringLength(20)] public string? PhoneNumber { get; set; }
}

public class UserResponse
{
    public int UserId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? ProfileImageUrl { get; set; }
    public DateTime CreatedAt { get; set; }

    public static UserResponse From(User u) => new()
    {
        UserId = u.UserId,
        FirstName = u.FirstName,
        LastName = u.LastName,
        Email = u.Email,
        Role = u.Role,
        PhoneNumber = u.PhoneNumber,
        ProfileImageUrl = u.ProfileImageUrl,
        CreatedAt = u.CreatedAt
    };
}
