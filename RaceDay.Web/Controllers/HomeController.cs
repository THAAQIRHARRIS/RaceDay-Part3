using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Models;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

public class HomeController : Controller
{
    private readonly ApiClient _api;

    public HomeController(ApiClient api) => _api = api;

    /// <summary>Upcoming events with filters (public).</summary>
    [HttpGet]
    public async Task<IActionResult> Index(string? q, string? type, string? distance)
    {
        var url = "api/events";
        if (!string.IsNullOrWhiteSpace(type))
            url += $"?type={Uri.EscapeDataString(type)}";

        var result = await _api.GetAsync<List<EventDto>>(url);
        if (!result.Success) ViewBag.Error = result.Error;

        var events = result.Data ?? new List<EventDto>();

        // Text and distance filters only change what is displayed.
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            events = events.Where(e =>
                e.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                e.Location.Contains(term, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        events = distance switch
        {
            "short" => events.Where(e => e.DistanceKm <= 10).ToList(),
            "medium" => events.Where(e => e.DistanceKm > 10 && e.DistanceKm <= 25).ToList(),
            "long" => events.Where(e => e.DistanceKm > 25 && e.DistanceKm <= 50).ToList(),
            "ultra" => events.Where(e => e.DistanceKm > 50).ToList(),
            _ => events
        };

        return View(new HomeIndexViewModel { Events = events, Q = q, Type = type, Distance = distance });
    }

    [HttpGet]
    public IActionResult Unavailable() => View();

    // The exception handler and status-code pages re-run the original request, so they must skip the CSRF check.
    [IgnoreAntiforgeryToken]
    public IActionResult Error() => View();

    [IgnoreAntiforgeryToken]
    public IActionResult Status(int id) => id == 404 ? View("NotFound") : View("Error");
}
