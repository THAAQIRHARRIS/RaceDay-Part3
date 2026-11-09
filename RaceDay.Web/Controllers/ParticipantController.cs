using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Filters;
using RaceDay.Web.Models;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

[RoleAuthorize(Roles.Participant)]
public class ParticipantController : Controller
{
    private readonly ApiClient _api;

    public ParticipantController(ApiClient api) => _api = api;

    /// <summary>All events the participant has entered, with colour-coded status.</summary>
    [HttpGet]
    public async Task<IActionResult> Enrolments()
    {
        var result = await _api.GetAsync<List<EnrolmentDto>>("api/enrolments/me");
        if (!result.Success) ViewBag.Error = result.Error;
        return View(result.Data ?? new List<EnrolmentDto>());
    }

    [HttpPost]
    public async Task<IActionResult> CancelEnrolment(int id)
    {
        var result = await _api.DeleteAsync<EmptyResponse>($"api/enrolments/{id}");
        if (result.Success)
            TempData["Success"] = "Your entry has been withdrawn.";
        else
            TempData["Error"] = result.Error;
        return RedirectToAction(nameof(Enrolments));
    }

    /// <summary>Personal race history: event, date, category, finish time and position (e.g. 47th out of 312 finishers).</summary>
    [HttpGet]
    public async Task<IActionResult> Results()
    {
        var result = await _api.GetAsync<List<ResultDto>>("api/results/me");
        if (!result.Success) ViewBag.Error = result.Error;
        return View(result.Data ?? new List<ResultDto>());
    }
}
