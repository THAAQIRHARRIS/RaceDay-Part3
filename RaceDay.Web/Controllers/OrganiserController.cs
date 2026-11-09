using System.Globalization;
using System.Net;
using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Filters;
using RaceDay.Web.Models;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

[RoleAuthorize(Roles.Organiser)]
public class OrganiserController : Controller
{
    private readonly ApiClient _api;

    public OrganiserController(ApiClient api) => _api = api;

    // -------------------------------------------------------------- dashboard

    /// <summary>Summary of the organiser's events, enrolments per event and upcoming dates.</summary>
    [HttpGet]
    public async Task<IActionResult> Dashboard()
    {
        var result = await _api.GetAsync<List<EventDto>>("api/events/mine");
        if (!result.Success) ViewBag.Error = result.Error;
        return View(new DashboardViewModel { Events = result.Data ?? new List<EventDto>() });
    }

    // ----------------------------------------------------------------- events

    [HttpGet]
    public async Task<IActionResult> Events()
    {
        var result = await _api.GetAsync<List<EventDto>>("api/events/mine");
        if (!result.Success) ViewBag.Error = result.Error;
        return View(result.Data ?? new List<EventDto>());
    }

    [HttpGet]
    public IActionResult CreateEvent() =>
        View("EventForm", new EventFormViewModel { EventDate = DateTime.Today.AddDays(30).AddHours(7) });

    [HttpPost]
    public async Task<IActionResult> CreateEvent(EventFormViewModel model)
    {
        if (!ModelState.IsValid) return View("EventForm", model);

        var result = await _api.PostAsync<EventDto>("api/events", ToRequest(model));
        if (!result.Success || result.Data == null)
        {
            ModelState.AddModelError(string.Empty, result.Error);
            return View("EventForm", model);
        }

        if (model.BannerFile is { Length: > 0 })
        {
            var upload = await _api.UploadImageAsync($"api/events/{result.Data.EventId}/banner", model.BannerFile);
            if (!upload.Success)
            {
                TempData["Warning"] = $"The event was created, but the banner could not be uploaded: {upload.Error}";
                return RedirectToAction(nameof(EditEvent), new { id = result.Data.EventId });
            }
        }

        TempData["Success"] = "Your event has been created. Next, add categories so participants can enter.";
        return RedirectToAction(nameof(Categories), new { eventId = result.Data.EventId });
    }

    [HttpGet]
    public async Task<IActionResult> EditEvent(int id)
    {
        var (ev, failure) = await LoadOwnedEventAsync(id);
        if (failure != null) return failure;

        return View("EventForm", new EventFormViewModel
        {
            EventId = ev!.EventId,
            Name = ev.Name,
            Description = ev.Description,
            EventDate = ev.EventDate,
            Location = ev.Location,
            DistanceKm = ev.DistanceKm,
            EventTypeId = ev.EventTypeId,
            ExistingBannerUrl = ev.BannerImageUrl
        });
    }

    [HttpPost]
    public async Task<IActionResult> EditEvent(int id, EventFormViewModel model)
    {
        model.EventId = id;
        if (!ModelState.IsValid) return View("EventForm", model);

        var result = await _api.PutAsync<EventDto>($"api/events/{id}", ToRequest(model));
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.Error);
            return View("EventForm", model);
        }

        if (model.BannerFile is { Length: > 0 })
        {
            var upload = await _api.UploadImageAsync($"api/events/{id}/banner", model.BannerFile);
            if (!upload.Success)
            {
                TempData["Warning"] = $"The event was saved, but the banner could not be uploaded: {upload.Error}";
                return RedirectToAction(nameof(EditEvent), new { id });
            }
        }

        TempData["Success"] = "Your event has been updated.";
        return RedirectToAction(nameof(Events));
    }

    [HttpPost]
    public async Task<IActionResult> DeleteEvent(int id)
    {
        var result = await _api.DeleteAsync<EmptyResponse>($"api/events/{id}");
        if (result.Success)
            TempData["Success"] = "The event has been deleted.";
        else
            TempData["Error"] = result.Error;
        return RedirectToAction(nameof(Events));
    }

    // ------------------------------------------------------------- categories

    [HttpGet]
    public async Task<IActionResult> Categories(int eventId)
    {
        var (ev, failure) = await LoadOwnedEventAsync(eventId);
        if (failure != null) return failure;

        return View(new CategoriesPageViewModel { Event = ev! });
    }

    [HttpPost]
    public async Task<IActionResult> AddCategory(int eventId, [Bind(Prefix = "Form")] CategoryFormViewModel form)
    {
        if (form.MinAge.HasValue && form.MaxAge.HasValue && form.MinAge > form.MaxAge)
            ModelState.AddModelError("Form.MinAge", "The minimum age cannot be higher than the maximum age.");

        if (!ModelState.IsValid)
        {
            var (ev, failure) = await LoadOwnedEventAsync(eventId);
            if (failure != null) return failure;
            return View("Categories", new CategoriesPageViewModel { Event = ev!, Form = form });
        }

        var result = await _api.PostAsync<CategoryDto>($"api/events/{eventId}/categories", new
        {
            categoryName = form.CategoryName,
            minAge = form.MinAge,
            maxAge = form.MaxAge,
            distanceKm = form.DistanceKm
        });

        if (result.Success)
            TempData["Success"] = $"Category \"{form.CategoryName}\" added.";
        else
            TempData["Error"] = result.Error;

        return RedirectToAction(nameof(Categories), new { eventId });
    }

    [HttpPost]
    public async Task<IActionResult> DeleteCategory(int eventId, int id)
    {
        var result = await _api.DeleteAsync<EmptyResponse>($"api/categories/{id}");
        if (result.Success)
            TempData["Success"] = "The category has been removed.";
        else
            TempData["Error"] = result.Error;
        return RedirectToAction(nameof(Categories), new { eventId });
    }

    // ------------------------------------------------------------ enrolments

    [HttpGet]
    public async Task<IActionResult> Enrolments(int eventId)
    {
        var (ev, failure) = await LoadOwnedEventAsync(eventId);
        if (failure != null) return failure;

        var enrolments = await _api.GetAsync<List<EnrolmentDto>>($"api/events/{eventId}/enrolments");
        if (!enrolments.Success) ViewBag.Error = enrolments.Error;

        return View(new EnrolmentsPageViewModel { Event = ev!, Enrolments = enrolments.Data ?? new List<EnrolmentDto>() });
    }

    [HttpPost]
    public async Task<IActionResult> UpdateEnrolmentStatus(int eventId, int enrolmentId, string status)
    {
        var result = await _api.PutAsync<EnrolmentDto>($"api/enrolments/{enrolmentId}/status", new { status });
        if (result.Success)
            TempData["Success"] = $"Enrolment marked as {status}.";
        else
            TempData["Error"] = result.Error;
        return RedirectToAction(nameof(Enrolments), new { eventId });
    }

    // --------------------------------------------------------------- results

    [HttpGet]
    public async Task<IActionResult> Results(int eventId)
    {
        var (ev, failure) = await LoadOwnedEventAsync(eventId);
        if (failure != null) return failure;

        var enrolments = await _api.GetAsync<List<EnrolmentDto>>($"api/events/{eventId}/enrolments");
        var results = await _api.GetAsync<List<ResultDto>>($"api/events/{eventId}/results");
        if (!enrolments.Success) ViewBag.Error = enrolments.Error;

        var byEnrolment = (results.Data ?? new List<ResultDto>()).ToDictionary(r => r.EnrolmentId);

        var rows = (enrolments.Data ?? new List<EnrolmentDto>())
            .Where(e => e.EnrolmentStatus != "Cancelled")
            .Select(e =>
            {
                byEnrolment.TryGetValue(e.EnrolmentId, out var r);
                return new ResultRowViewModel
                {
                    EnrolmentId = e.EnrolmentId,
                    ParticipantName = e.ParticipantName,
                    CategoryName = e.CategoryName,
                    EnrolmentStatus = e.EnrolmentStatus,
                    ResultId = r?.ResultId,
                    FinishTime = r?.FinishTime,
                    FinishPosition = r?.FinishPosition,
                    TotalFinishers = r?.TotalFinishers
                };
            })
            .OrderBy(r => r.FinishPosition ?? int.MaxValue)
            .ThenBy(r => r.ParticipantName)
            .ToList();

        return View(new ResultsPageViewModel
        {
            Event = ev!,
            Rows = rows,
            EventHasPassed = ev!.EventDate <= DateTime.UtcNow
        });
    }

    /// <summary>Captures (or corrects) a result. Saving publishes it to the participant's My Results page straight away.</summary>
    [HttpPost]
    public async Task<IActionResult> SaveResult(ResultFormViewModel model)
    {
        var back = RedirectToAction(nameof(Results), new { eventId = model.EventId });

        if (!TimeSpan.TryParseExact(model.FinishTime?.Trim(), new[] { @"h\:mm\:ss", @"hh\:mm\:ss" },
                CultureInfo.InvariantCulture, out var finish) || finish <= TimeSpan.Zero)
        {
            TempData["Error"] = "Enter the finish time as hours:minutes:seconds, for example 03:45:12.";
            return back;
        }

        if (model.FinishPosition is null or < 1 || model.TotalFinishers is null or < 1)
        {
            TempData["Error"] = "Enter the finishing position and the total number of finishers (both must be 1 or more).";
            return back;
        }

        if (model.FinishPosition > model.TotalFinishers)
        {
            TempData["Error"] = "The finishing position cannot be higher than the total number of finishers.";
            return back;
        }

        var body = new
        {
            finishTime = finish.ToString("c", CultureInfo.InvariantCulture),
            finishPosition = model.FinishPosition,
            totalFinishers = model.TotalFinishers
        };

        var result = model.ResultId.HasValue
            ? await _api.PutAsync<ResultDto>($"api/results/{model.ResultId.Value}", body)
            : await _api.PostAsync<ResultDto>($"api/enrolments/{model.EnrolmentId}/result", body);

        if (result.Success)
            TempData["Success"] = "Result saved and published.";
        else
            TempData["Error"] = result.Error;

        return back;
    }

    // ---------------------------------------------------------------- helpers

    private static object ToRequest(EventFormViewModel m) => new
    {
        name = m.Name,
        description = m.Description,
        eventDate = m.EventDate,
        location = m.Location,
        distanceKm = m.DistanceKm,
        eventTypeId = m.EventTypeId
    };

    /// <summary>Loads an event and makes sure it belongs to the logged-in organiser.</summary>
    private async Task<(EventDto? Event, IActionResult? Failure)> LoadOwnedEventAsync(int eventId)
    {
        var result = await _api.GetAsync<EventDto>($"api/events/{eventId}");
        if (result.Status == HttpStatusCode.NotFound) return (null, NotFound());

        if (!result.Success || result.Data == null)
        {
            TempData["Error"] = result.Error;
            return (null, RedirectToAction(nameof(Events)));
        }

        if (result.Data.OrganiserId != HttpContext.Session.CurrentUserId())
            return (null, RedirectToAction("AccessDenied", "Account"));

        return (result.Data, null);
    }
}
