using System.Linq.Expressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Auth;
using RaceDay.Api.Data;
using RaceDay.Api.DTOs;
using RaceDay.Api.Models;

namespace RaceDay.Api.Controllers;

[Route("api/events")]
public class EventsController : ApiControllerBase
{
    private readonly RaceDayDbContext _db;
    public EventsController(RaceDayDbContext db) => _db = db;

    private static readonly Expression<Func<Event, EventResponse>> ToResponse = e => new EventResponse
    {
        EventId = e.EventId,
        OrganiserId = e.OrganiserId,
        OrganiserName = e.Organiser.FirstName + " " + e.Organiser.LastName,
        Name = e.Name,
        Description = e.Description,
        EventDate = e.EventDate,
        Location = e.Location,
        DistanceKm = e.DistanceKm,
        EventTypeId = e.EventTypeId,
        EventType = e.EventType.TypeName,
        BannerImageUrl = e.BannerImageUrl,
        EnrolmentCount = e.Enrolments.Count
    };

    /// <summary>List events (upcoming by default).</summary>
    /// <remarks>
    /// Role required: none (public). Optional filters: <c>type</c> (Run, Walk or Cycle) and
    /// <c>includePast=true</c> to also return events that have already happened.
    /// </remarks>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<EventResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<EventResponse>>> GetEvents([FromQuery] bool includePast = false, [FromQuery] string? type = null)
    {
        var query = _db.Events.AsNoTracking().AsQueryable();
        if (!includePast) query = query.Where(e => e.EventDate >= DateTime.UtcNow);
        if (!string.IsNullOrWhiteSpace(type)) query = query.Where(e => e.EventType.TypeName == type);

        return Ok(await query.OrderBy(e => e.EventDate).Select(ToResponse).ToListAsync());
    }

    /// <summary>List every event created by the logged-in Organiser (past and upcoming).</summary>
    /// <remarks>Role required: Organiser. Used for the organiser dashboard.</remarks>
    [HttpGet("mine")]
    [SessionAuthorize(Roles.Organiser)]
    [ProducesResponseType(typeof(IEnumerable<EventResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IEnumerable<EventResponse>>> GetMyEvents()
    {
        var events = await _db.Events.AsNoTracking()
            .Where(e => e.OrganiserId == CurrentUserId)
            .OrderBy(e => e.EventDate)
            .Select(ToResponse)
            .ToListAsync();
        return Ok(events);
    }

    /// <summary>Get full details of one event, including its categories.</summary>
    /// <remarks>Role required: none (public).</remarks>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(EventResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EventResponse>> GetEvent(int id)
    {
        var response = await _db.Events.AsNoTracking().Where(e => e.EventId == id).Select(ToResponse).FirstOrDefaultAsync();
        if (response == null) return Fail(StatusCodes.Status404NotFound, "Event not found.");

        var categories = await _db.Categories.AsNoTracking().Where(c => c.EventId == id).OrderBy(c => c.CategoryId).ToListAsync();
        response.Categories = categories.Select(CategoryResponse.From).ToList();
        return Ok(response);
    }

    /// <summary>Create a new event owned by the logged-in Organiser.</summary>
    /// <remarks>Role required: Organiser. eventTypeId: 1 = Run, 2 = Walk, 3 = Cycle.</remarks>
    [HttpPost]
    [SessionAuthorize(Roles.Organiser)]
    [ProducesResponseType(typeof(EventResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<EventResponse>> CreateEvent(EventRequest request)
    {
        if (!await _db.EventTypes.AnyAsync(t => t.EventTypeId == request.EventTypeId))
            return Fail(StatusCodes.Status400BadRequest, "Invalid eventTypeId. Use 1 (Run), 2 (Walk) or 3 (Cycle).");

        var ev = new Event
        {
            OrganiserId = CurrentUserId,
            EventTypeId = request.EventTypeId,
            Name = request.Name.Trim(),
            Description = request.Description,
            EventDate = request.EventDate!.Value,
            Location = request.Location.Trim(),
            DistanceKm = request.DistanceKm
        };
        _db.Events.Add(ev);
        await _db.SaveChangesAsync();

        var response = await _db.Events.AsNoTracking().Where(e => e.EventId == ev.EventId).Select(ToResponse).FirstAsync();
        return CreatedAtAction(nameof(GetEvent), new { id = ev.EventId }, response);
    }

    /// <summary>Update an event. Only the Organiser who created it may do this.</summary>
    /// <remarks>Role required: Organiser (owner of the event).</remarks>
    [HttpPut("{id:int}")]
    [SessionAuthorize(Roles.Organiser)]
    [ProducesResponseType(typeof(EventResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EventResponse>> UpdateEvent(int id, EventRequest request)
    {
        var ev = await _db.Events.FirstOrDefaultAsync(e => e.EventId == id);
        if (ev == null) return Fail(StatusCodes.Status404NotFound, "Event not found.");
        if (ev.OrganiserId != CurrentUserId) return Fail(StatusCodes.Status403Forbidden, "You can only edit your own events.");
        if (!await _db.EventTypes.AnyAsync(t => t.EventTypeId == request.EventTypeId))
            return Fail(StatusCodes.Status400BadRequest, "Invalid eventTypeId. Use 1 (Run), 2 (Walk) or 3 (Cycle).");

        ev.Name = request.Name.Trim();
        ev.Description = request.Description;
        ev.EventDate = request.EventDate!.Value;
        ev.Location = request.Location.Trim();
        ev.DistanceKm = request.DistanceKm;
        ev.EventTypeId = request.EventTypeId;
        await _db.SaveChangesAsync();

        return Ok(await _db.Events.AsNoTracking().Where(e => e.EventId == id).Select(ToResponse).FirstAsync());
    }

    /// <summary>Delete an event, together with its categories, enrolments and results.</summary>
    /// <remarks>Role required: Organiser (owner of the event).</remarks>
    [HttpDelete("{id:int}")]
    [SessionAuthorize(Roles.Organiser)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteEvent(int id)
    {
        var ev = await _db.Events.Include(e => e.Enrolments).FirstOrDefaultAsync(e => e.EventId == id);
        if (ev == null) return Fail(StatusCodes.Status404NotFound, "Event not found.");
        if (ev.OrganiserId != CurrentUserId) return Fail(StatusCodes.Status403Forbidden, "You can only delete your own events.");

        // Enrolments are RESTRICT in the schema, so remove them first. Results and categories cascade in the database.
        _db.EventEnrolments.RemoveRange(ev.Enrolments);
        _db.Events.Remove(ev);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
