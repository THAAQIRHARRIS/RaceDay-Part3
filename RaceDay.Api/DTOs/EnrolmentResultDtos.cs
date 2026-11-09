using System.ComponentModel.DataAnnotations;

namespace RaceDay.Api.DTOs;

public class EnrolRequest
{
    [Range(1, int.MaxValue)] public int CategoryId { get; set; }
}

public class UpdateEnrolmentStatusRequest
{
    /// <summary>Pending, Confirmed or Cancelled.</summary>
    [Required] public string Status { get; set; } = string.Empty;
}

public class EnrolmentResponse
{
    public int EnrolmentId { get; set; }
    public int EventId { get; set; }
    public string EventName { get; set; } = string.Empty;
    public DateTime EventDate { get; set; }
    public int ParticipantId { get; set; }
    public string ParticipantName { get; set; } = string.Empty;
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string EnrolmentStatus { get; set; } = string.Empty;
    public DateTime EnrolmentDate { get; set; }
}

public class ResultRequest
{
    /// <summary>Finish time as hh:mm:ss, e.g. 03:45:12.</summary>
    [Required] public TimeSpan? FinishTime { get; set; }

    /// <summary>Overall finishing position, e.g. 47.</summary>
    [Range(1, int.MaxValue)] public int FinishPosition { get; set; }

    /// <summary>Total number of finishers, e.g. 312.</summary>
    [Range(1, int.MaxValue)] public int TotalFinishers { get; set; }
}

public class ResultResponse
{
    public int ResultId { get; set; }
    public int EnrolmentId { get; set; }
    public int EventId { get; set; }
    public string EventName { get; set; } = string.Empty;
    public DateTime EventDate { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string ParticipantName { get; set; } = string.Empty;
    public TimeSpan? FinishTime { get; set; }
    public int? FinishPosition { get; set; }
    public int? TotalFinishers { get; set; }
    public DateTime? CapturedAt { get; set; }
}
