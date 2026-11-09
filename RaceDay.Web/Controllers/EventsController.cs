using System.Net;
using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Filters;
using RaceDay.Web.Models;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

public class EventsController : Controller
{
    private readonly ApiClient _api;

    public EventsController(ApiClient api) => _api = api;

    /// <summary>Full event information and categories (public). Participants also see their enrolment status.</summary>
    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var result = await _api.GetAsync<EventDto>($"api/events/{id}");
        if (result.Status == HttpStatusCode.NotFound) return NotFound();
        if (!result.Success || result.Data == null)
        {
            TempData["Error"] = result.Error;
            return RedirectToAction("Index", "Home");
        }

        var model = new EventDetailsViewModel { Event = result.Data };
        var session = HttpContext.Session;

        if (session.GetRole() == Roles.Organiser)
        {
            model.IsOwner = result.Data.OrganiserId == session.CurrentUserId();
        }
        else if (session.GetRole() == Roles.Participant)
        {
            var mine = await _api.GetAsync<List<EnrolmentDto>>("api/enrolments/me");
            model.MyEnrolment = mine.Data?.FirstOrDefault(e => e.EventId == id);
        }

        return View(model);
    }

    /// <summary>Enter an event: records the enrolment immediately (status Pending until the Organiser confirms).</summary>
    [HttpPost, RoleAuthorize(Roles.Participant)]
    public async Task<IActionResult> Enrol(int eventId, int categoryId)
    {
        if (categoryId <= 0)
        {
            TempData["Error"] = "Please choose a category before entering the event.";
            return RedirectToAction(nameof(Details), new { id = eventId });
        }

        var result = await _api.PostAsync<EnrolmentDto>($"api/events/{eventId}/enrolments", new { categoryId });
        if (!result.Success)
        {
            TempData["Error"] = result.Error;
            return RedirectToAction(nameof(Details), new { id = eventId });
        }

        TempData["Success"] = "You're in! Your entry has been recorded and is awaiting confirmation.";
        return RedirectToAction("Enrolments", "Participant");
    }
}
