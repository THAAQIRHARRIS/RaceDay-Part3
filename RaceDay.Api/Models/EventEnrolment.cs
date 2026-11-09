using System.ComponentModel.DataAnnotations;

namespace RaceDay.Api.Models;

public class EventEnrolment
{
    [Key]
    public int EnrolmentId { get; set; }
    public int ParticipantId { get; set; }
    public int EventId { get; set; }
    public int CategoryId { get; set; }
    [Required, MaxLength(20)] public string EnrolmentStatus { get; set; } = EnrolmentStatuses.Pending;
    public DateTime EnrolmentDate { get; set; }

    public User Participant { get; set; } = null!;
    public Event Event { get; set; } = null!;
    public Category Category { get; set; } = null!;
    public Result? Result { get; set; }
}

public static class EnrolmentStatuses
{
    public const string Pending = "Pending";
    public const string Confirmed = "Confirmed";
    public const string Cancelled = "Cancelled";

    public static readonly string[] All = { Pending, Confirmed, Cancelled };
}
