using System.ComponentModel.DataAnnotations;

namespace RaceDay.Api.Models;

public class Event
{
    public int EventId { get; set; }
    public int OrganiserId { get; set; }
    public int EventTypeId { get; set; }
    [Required, MaxLength(150)] public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime EventDate { get; set; }
    [Required, MaxLength(200)] public string Location { get; set; } = string.Empty;
    public decimal DistanceKm { get; set; }
    [MaxLength(500)] public string? BannerImageUrl { get; set; }
    public DateTime CreatedAt { get; set; }

    public User Organiser { get; set; } = null!;
    public EventType EventType { get; set; } = null!;
    public ICollection<Category> Categories { get; set; } = new List<Category>();
    public ICollection<EventEnrolment> Enrolments { get; set; } = new List<EventEnrolment>();
}
