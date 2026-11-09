using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Auth;
using RaceDay.Api.Data;
using RaceDay.Api.DTOs;
using RaceDay.Api.Models;

namespace RaceDay.Api.Controllers;

public class CategoriesController : ApiControllerBase
{
    private readonly RaceDayDbContext _db;
    public CategoriesController(RaceDayDbContext db) => _db = db;

    /// <summary>List all categories available for an event.</summary>
    /// <remarks>Role required: none (public).</remarks>
    [HttpGet("api/events/{eventId:int}/categories")]
    [ProducesResponseType(typeof(IEnumerable<CategoryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<CategoryResponse>>> GetCategories(int eventId)
    {
        if (!await _db.Events.AnyAsync(e => e.EventId == eventId))
            return Fail(StatusCodes.Status404NotFound, "Event not found.");

        var categories = await _db.Categories.AsNoTracking().Where(c => c.EventId == eventId).OrderBy(c => c.CategoryId).ToListAsync();
        return Ok(categories.Select(CategoryResponse.From));
    }

    /// <summary>Add an age or distance category to an event.</summary>
    /// <remarks>Role required: Organiser (owner of the event). Examples: "Under 20", "Senior", "21km".</remarks>
    [HttpPost("api/events/{eventId:int}/categories")]
    [SessionAuthorize(Roles.Organiser)]
    [ProducesResponseType(typeof(CategoryResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CategoryResponse>> CreateCategory(int eventId, CategoryRequest request)
    {
        var ev = await _db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.EventId == eventId);
        if (ev == null) return Fail(StatusCodes.Status404NotFound, "Event not found.");
        if (ev.OrganiserId != CurrentUserId) return Fail(StatusCodes.Status403Forbidden, "You can only add categories to your own events.");
        if (request.MinAge > request.MaxAge) return Fail(StatusCodes.Status400BadRequest, "MinAge cannot be greater than MaxAge.");

        var category = new Category
        {
            EventId = eventId,
            CategoryName = request.CategoryName.Trim(),
            MinAge = request.MinAge,
            MaxAge = request.MaxAge,
            DistanceKm = request.DistanceKm
        };
        _db.Categories.Add(category);
        await _db.SaveChangesAsync();

        return Created($"/api/events/{eventId}/categories", CategoryResponse.From(category));
    }

    /// <summary>Update a category.</summary>
    /// <remarks>Role required: Organiser (owner of the event the category belongs to).</remarks>
    [HttpPut("api/categories/{id:int}")]
    [SessionAuthorize(Roles.Organiser)]
    [ProducesResponseType(typeof(CategoryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CategoryResponse>> UpdateCategory(int id, CategoryRequest request)
    {
        var category = await _db.Categories.Include(c => c.Event).FirstOrDefaultAsync(c => c.CategoryId == id);
        if (category == null) return Fail(StatusCodes.Status404NotFound, "Category not found.");
        if (category.Event.OrganiserId != CurrentUserId) return Fail(StatusCodes.Status403Forbidden, "You can only edit categories of your own events.");
        if (request.MinAge > request.MaxAge) return Fail(StatusCodes.Status400BadRequest, "MinAge cannot be greater than MaxAge.");

        category.CategoryName = request.CategoryName.Trim();
        category.MinAge = request.MinAge;
        category.MaxAge = request.MaxAge;
        category.DistanceKm = request.DistanceKm;
        await _db.SaveChangesAsync();

        return Ok(CategoryResponse.From(category));
    }

    /// <summary>Delete a category that nobody has enrolled in.</summary>
    /// <remarks>Role required: Organiser (owner of the event). Returns 409 if participants are already enrolled in it.</remarks>
    [HttpDelete("api/categories/{id:int}")]
    [SessionAuthorize(Roles.Organiser)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteCategory(int id)
    {
        var category = await _db.Categories.Include(c => c.Event).FirstOrDefaultAsync(c => c.CategoryId == id);
        if (category == null) return Fail(StatusCodes.Status404NotFound, "Category not found.");
        if (category.Event.OrganiserId != CurrentUserId) return Fail(StatusCodes.Status403Forbidden, "You can only delete categories of your own events.");
        if (await _db.EventEnrolments.AnyAsync(en => en.CategoryId == id))
            return Fail(StatusCodes.Status409Conflict, "This category has enrolments and cannot be deleted.");

        _db.Categories.Remove(category);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
