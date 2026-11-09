namespace RaceDay.Api.Models;

public class Result
{
    public int ResultId { get; set; }
    public int EnrolmentId { get; set; }
    public TimeSpan? FinishTime { get; set; }
    public int? FinishPosition { get; set; }
    public int? TotalFinishers { get; set; }
    public DateTime? CapturedAt { get; set; }

    public EventEnrolment Enrolment { get; set; } = null!;
}
