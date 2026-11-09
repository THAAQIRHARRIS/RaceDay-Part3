using System.ComponentModel.DataAnnotations;

namespace RaceDay.Api.Models;

public class User
{
    public int UserId { get; set; }
    [Required, MaxLength(100)] public string FirstName { get; set; } = string.Empty;
    [Required, MaxLength(100)] public string LastName { get; set; } = string.Empty;
    [Required, MaxLength(150)] public string Email { get; set; } = string.Empty;
    [Required, MaxLength(256)] public string PasswordHash { get; set; } = string.Empty;
    [Required, MaxLength(20)] public string Role { get; set; } = string.Empty;
    [MaxLength(20)] public string? PhoneNumber { get; set; }
    [MaxLength(500)] public string? ProfileImageUrl { get; set; }
    public DateTime CreatedAt { get; set; }

    public ICollection<Event> OrganisedEvents { get; set; } = new List<Event>();
    public ICollection<EventEnrolment> Enrolments { get; set; } = new List<EventEnrolment>();
}
