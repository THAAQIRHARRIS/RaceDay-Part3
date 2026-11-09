using System.Linq.Expressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Auth;
using RaceDay.Api.Data;
using RaceDay.Api.DTOs;
using RaceDay.Api.Models;

namespace RaceDay.Api.Controllers;

public class ResultsController : ApiControllerBase
{
    private readonly RaceDayDbContext _db;
    public ResultsController(RaceDayDbContext db) => _db = db;

    private static readonly Expression<Func<Result, ResultResponse>> ToResponse = r => new ResultResponse
    {
        ResultId = r.ResultId,
        EnrolmentId = r.EnrolmentId,
        EventId = r.Enrolment.EventId,
        EventName = r.Enrolment.Event.Name,
        EventDate = r.Enrolment.Event.EventDate,
        CategoryName = r.Enrolment.Category.CategoryName,
        ParticipantName = r.Enrolment.Participant.FirstName + " " + r.Enrolment.Participant.LastName,
        FinishTime = r.FinishTime,
        FinishPosition = r.FinishPosition,
        TotalFinishers = r.TotalFinishers,
        CapturedAt = r.CapturedAt
    };

    /// <summary>Capture the finish time and finishing position for an enrolled participant.</summary>
    /// <remarks>
    /// Role required: Organiser (owner of the event). Only possible once the event date has passed.
    /// Returns 409 if a result has already been captured for this enrolment (use PUT to correct it).
    /// </remarks>
    [HttpPost("api/enrolments/{enrolmentId:int}/result")]
    [SessionAuthorize(Roles.Organiser)]
    [ProducesResponseType(typeof(ResultResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ResultResponse>> CaptureResult(int enrolmentId, ResultRequest request)
    {
        var enrolment = await _db.EventEnrolments.AsNoTracking().Include(en => en.Event)
            .FirstOrDefaultAsync(en => en.EnrolmentId == enrolmentId);
        if (enrolment == null) return Fail(StatusCodes.Status404NotFound, "Enrolment not found.");
        if (enrolment.Event.OrganiserId != CurrentUserId) return Fail(StatusCodes.Status403Forbidden, "You can only capture results for your own events.");
        if (enrolment.EnrolmentStatus == EnrolmentStatuses.Cancelled) return Fail(StatusCodes.Status400BadRequest, "This enrolment was cancelled.");
        if (enrolment.Event.EventDate > DateTime.UtcNow) return Fail(StatusCodes.Status400BadRequest, "Results can only be captured after the event has taken place.");

        var invalid = Validate(request);
        if (invalid != null) return invalid;

        if (await _db.Results.AnyAsync(r => r.EnrolmentId == enrolmentId))
            return Fail(StatusCodes.Status409Conflict, "A result already exists for this enrolment. Use PUT /api/results/{id} to update it.");

        var result = new Result
        {
            EnrolmentId = enrolmentId,
            FinishTime = request.FinishTime,
            FinishPosition = request.FinishPosition,
            TotalFinishers = request.TotalFinishers,
            CapturedAt = DateTime.UtcNow
        };
        _db.Results.Add(result);
        await _db.SaveChangesAsync();

        var response = await _db.Results.AsNoTracking().Where(r => r.ResultId == result.ResultId).Select(ToResponse).FirstAsync();
        return Created($"/api/events/{enrolment.EventId}/results", response);
    }

    /// <summary>Correct a previously captured result.</summary>
    /// <remarks>Role required: Organiser (owner of the event).</remarks>
    [HttpPut("api/results/{id:int}")]
    [SessionAuthorize(Roles.Organiser)]
    [ProducesResponseType(typeof(ResultResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ResultResponse>> UpdateResult(int id, ResultRequest request)
    {
        var result = await _db.Results.Include(r => r.Enrolment).ThenInclude(en => en.Event)
            .FirstOrDefaultAsync(r => r.ResultId == id);
        if (result == null) return Fail(StatusCodes.Status404NotFound, "Result not found.");
        if (result.Enrolment.Event.OrganiserId != CurrentUserId) return Fail(StatusCodes.Status403Forbidden, "You can only edit results for your own events.");

        var invalid = Validate(request);
        if (invalid != null) return invalid;

        result.FinishTime = request.FinishTime;
        result.FinishPosition = request.FinishPosition;
        result.TotalFinishers = request.TotalFinishers;
        result.CapturedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(await _db.Results.AsNoTracking().Where(r => r.ResultId == id).Select(ToResponse).FirstAsync());
    }

    /// <summary>Get the logged-in Participant's personal race history, newest event first.</summary>
    /// <remarks>Role required: Participant. Each entry includes event name and date, category, finish time and position (e.g. 47 of 312).</remarks>
    [HttpGet("api/results/me")]
    [SessionAuthorize(Roles.Participant)]
    [ProducesResponseType(typeof(IEnumerable<ResultResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IEnumerable<ResultResponse>>> GetMyResults()
    {
        var results = await _db.Results.AsNoTracking()
            .Where(r => r.Enrolment.ParticipantId == CurrentUserId)
            .OrderByDescending(r => r.Enrolment.Event.EventDate)
            .Select(ToResponse)
            .ToListAsync();
        return Ok(results);
    }

    /// <summary>List all results captured for an event, ordered by finishing position.</summary>
    /// <remarks>Role required: none (public results board).</remarks>
    [HttpGet("api/events/{eventId:int}/results")]
    [ProducesResponseType(typeof(IEnumerable<ResultResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<ResultResponse>>> GetEventResults(int eventId)
    {
        if (!await _db.Events.AnyAsync(e => e.EventId == eventId))
            return Fail(StatusCodes.Status404NotFound, "Event not found.");

        var results = await _db.Results.AsNoTracking()
            .Where(r => r.Enrolment.EventId == eventId)
            .OrderBy(r => r.FinishPosition)
            .Select(ToResponse)
            .ToListAsync();
        return Ok(results);
    }

    private ObjectResult? Validate(ResultRequest request)
    {
        if (request.FinishTime!.Value <= TimeSpan.Zero)
            return Fail(StatusCodes.Status400BadRequest, "Finish time must be greater than zero.");
        if (request.FinishPosition > request.TotalFinishers)
            return Fail(StatusCodes.Status400BadRequest, "Finish position cannot be greater than the total number of finishers.");
        return null;
    }
}
