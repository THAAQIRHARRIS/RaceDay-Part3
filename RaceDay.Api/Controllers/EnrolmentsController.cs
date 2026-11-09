using System.Linq.Expressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Auth;
using RaceDay.Api.Data;
using RaceDay.Api.DTOs;
using RaceDay.Api.Models;

namespace RaceDay.Api.Controllers;

public class EnrolmentsController : ApiControllerBase
{
    private readonly RaceDayDbContext _db;
    public EnrolmentsController(RaceDayDbContext db) => _db = db;

    private static readonly Expression<Func<EventEnrolment, EnrolmentResponse>> ToResponse = en => new EnrolmentResponse
    {
        EnrolmentId = en.EnrolmentId,
        EventId = en.EventId,
        EventName = en.Event.Name,
        EventDate = en.Event.EventDate,
        ParticipantId = en.ParticipantId,
        ParticipantName = en.Participant.FirstName + " " + en.Participant.LastName,
        CategoryId = en.CategoryId,
        CategoryName = en.Category.CategoryName,
        EnrolmentStatus = en.EnrolmentStatus,
        EnrolmentDate = en.EnrolmentDate
    };

    /// <summary>Enter an event by choosing one of its categories.</summary>
    /// <remarks>
    /// Role required: Participant. The enrolment links the participant, the event and the category,
    /// and starts as Pending. Returns 409 if the participant is already enrolled in this event.
    /// </remarks>
    [HttpPost("api/events/{eventId:int}/enrolments")]
    [SessionAuthorize(Roles.Participant)]
    [ProducesResponseType(typeof(EnrolmentResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<EnrolmentResponse>> Enrol(int eventId, EnrolRequest request)
    {
        var ev = await _db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.EventId == eventId);
        if (ev == null) return Fail(StatusCodes.Status404NotFound, "Event not found.");
        if (ev.EventDate < DateTime.UtcNow) return Fail(StatusCodes.Status400BadRequest, "This event has already taken place.");

        var category = await _db.Categories.AsNoTracking()
            .FirstOrDefaultAsync(c => c.CategoryId == request.CategoryId && c.EventId == eventId);
        if (category == null) return Fail(StatusCodes.Status400BadRequest, "That category does not belong to this event.");

        if (await _db.EventEnrolments.AnyAsync(en => en.EventId == eventId && en.ParticipantId == CurrentUserId))
            return Fail(StatusCodes.Status409Conflict, "You are already enrolled in this event.");

        var enrolment = new EventEnrolment
        {
            ParticipantId = CurrentUserId,
            EventId = eventId,
            CategoryId = category.CategoryId,
            EnrolmentStatus = EnrolmentStatuses.Pending,
            EnrolmentDate = DateTime.UtcNow
        };
        _db.EventEnrolments.Add(enrolment);

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Two simultaneous requests can both pass the check above; the unique index catches the second.
            return Fail(StatusCodes.Status409Conflict, "You are already enrolled in this event.");
        }

        var response = await _db.EventEnrolments.AsNoTracking()
            .Where(en => en.EnrolmentId == enrolment.EnrolmentId).Select(ToResponse).FirstAsync();
        return Created("/api/enrolments/me", response);
    }

    /// <summary>List every event the logged-in Participant has enrolled in, with status.</summary>
    /// <remarks>Role required: Participant.</remarks>
    [HttpGet("api/enrolments/me")]
    [SessionAuthorize(Roles.Participant)]
    [ProducesResponseType(typeof(IEnumerable<EnrolmentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IEnumerable<EnrolmentResponse>>> GetMyEnrolments()
    {
        var list = await _db.EventEnrolments.AsNoTracking()
            .Where(en => en.ParticipantId == CurrentUserId)
            .OrderBy(en => en.Event.EventDate)
            .Select(ToResponse)
            .ToListAsync();
        return Ok(list);
    }

    /// <summary>List all participants enrolled in one of the Organiser's events.</summary>
    /// <remarks>Role required: Organiser (owner of the event).</remarks>
    [HttpGet("api/events/{eventId:int}/enrolments")]
    [SessionAuthorize(Roles.Organiser)]
    [ProducesResponseType(typeof(IEnumerable<EnrolmentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<EnrolmentResponse>>> GetEventEnrolments(int eventId)
    {
        var ev = await _db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.EventId == eventId);
        if (ev == null) return Fail(StatusCodes.Status404NotFound, "Event not found.");
        if (ev.OrganiserId != CurrentUserId) return Fail(StatusCodes.Status403Forbidden, "You can only view enrolments for your own events.");

        var list = await _db.EventEnrolments.AsNoTracking()
            .Where(en => en.EventId == eventId)
            .OrderBy(en => en.EnrolmentDate)
            .Select(ToResponse)
            .ToListAsync();
        return Ok(list);
    }

    /// <summary>Change an enrolment's status (Pending, Confirmed or Cancelled).</summary>
    /// <remarks>Role required: Organiser (owner of the event).</remarks>
    [HttpPut("api/enrolments/{id:int}/status")]
    [SessionAuthorize(Roles.Organiser)]
    [ProducesResponseType(typeof(EnrolmentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EnrolmentResponse>> UpdateStatus(int id, UpdateEnrolmentStatusRequest request)
    {
        var status = EnrolmentStatuses.All.FirstOrDefault(s => string.Equals(s, request.Status, StringComparison.OrdinalIgnoreCase));
        if (status == null) return Fail(StatusCodes.Status400BadRequest, "Status must be Pending, Confirmed or Cancelled.");

        var enrolment = await _db.EventEnrolments.Include(en => en.Event).FirstOrDefaultAsync(en => en.EnrolmentId == id);
        if (enrolment == null) return Fail(StatusCodes.Status404NotFound, "Enrolment not found.");
        if (enrolment.Event.OrganiserId != CurrentUserId) return Fail(StatusCodes.Status403Forbidden, "You can only manage enrolments for your own events.");

        enrolment.EnrolmentStatus = status;
        await _db.SaveChangesAsync();

        return Ok(await _db.EventEnrolments.AsNoTracking().Where(en => en.EnrolmentId == id).Select(ToResponse).FirstAsync());
    }

    /// <summary>Withdraw (delete) the logged-in Participant's own enrolment.</summary>
    /// <remarks>Role required: Participant (owner of the enrolment).</remarks>
    [HttpDelete("api/enrolments/{id:int}")]
    [SessionAuthorize(Roles.Participant)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CancelEnrolment(int id)
    {
        var enrolment = await _db.EventEnrolments.FirstOrDefaultAsync(en => en.EnrolmentId == id);
        if (enrolment == null) return Fail(StatusCodes.Status404NotFound, "Enrolment not found.");
        if (enrolment.ParticipantId != CurrentUserId) return Fail(StatusCodes.Status403Forbidden, "You can only cancel your own enrolments.");

        _db.EventEnrolments.Remove(enrolment);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
