using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace RaceDay.Web.Models;

// ---------------------------------------------------------------- account

public class LoginViewModel
{
    [Required(ErrorMessage = "Please enter your email address."), EmailAddress(ErrorMessage = "That doesn't look like a valid email address.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please enter your password."), DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    public string? ReturnUrl { get; set; }
}

public class RegisterViewModel
{
    [Required(ErrorMessage = "Please enter your first name."), StringLength(100), Display(Name = "First name")]
    public string FirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please enter your last name."), StringLength(100), Display(Name = "Last name")]
    public string LastName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please enter your email address."), EmailAddress(ErrorMessage = "That doesn't look like a valid email address."), StringLength(150)]
    public string Email { get; set; } = string.Empty;

    [Phone(ErrorMessage = "That doesn't look like a valid phone number."), StringLength(20), Display(Name = "Phone number")]
    public string? PhoneNumber { get; set; }

    [Required(ErrorMessage = "Please choose a password."), StringLength(100, MinimumLength = 8, ErrorMessage = "Your password must be at least 8 characters."), DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please confirm your password."), Compare(nameof(Password), ErrorMessage = "The passwords do not match."), DataType(DataType.Password), Display(Name = "Confirm password")]
    public string ConfirmPassword { get; set; } = string.Empty;

    [Required] public string Role { get; set; } = "Participant";

    [Display(Name = "Organiser code")]
    public string? OrganiserCode { get; set; }
}

public class ProfileViewModel
{
    [Required(ErrorMessage = "Please enter your first name."), StringLength(100), Display(Name = "First name")]
    public string FirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please enter your last name."), StringLength(100), Display(Name = "Last name")]
    public string LastName { get; set; } = string.Empty;

    [Phone(ErrorMessage = "That doesn't look like a valid phone number."), StringLength(20), Display(Name = "Phone number")]
    public string? PhoneNumber { get; set; }

    // Display only (not editable)
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string? ProfileImageUrl { get; set; }
}

// ------------------------------------------------------------- public pages

public class HomeIndexViewModel
{
    public List<EventDto> Events { get; set; } = new();
    public string? Q { get; set; }
    public string? Type { get; set; }
    public string? Distance { get; set; }
}

public class EventDetailsViewModel
{
    public EventDto Event { get; set; } = new();
    public EnrolmentDto? MyEnrolment { get; set; }
    public bool IsOwner { get; set; }
    public bool HasPassed => Event.EventDate < DateTime.UtcNow;
}

// ---------------------------------------------------------------- organiser

public class EventFormViewModel
{
    public int? EventId { get; set; }

    [Required(ErrorMessage = "Please enter the event name."), StringLength(150), Display(Name = "Event name")]
    public string Name { get; set; } = string.Empty;

    [StringLength(4000)]
    public string? Description { get; set; }

    [Required(ErrorMessage = "Please choose the date and time."), Display(Name = "Date and time")]
    public DateTime? EventDate { get; set; }

    [Required(ErrorMessage = "Please enter the location."), StringLength(200)]
    public string Location { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please enter the distance."), Range(0.01, 1000, ErrorMessage = "The distance must be between 0.01 and 1000 km."), Display(Name = "Distance (km)")]
    public decimal? DistanceKm { get; set; }

    [Required(ErrorMessage = "Please choose an event type."), Range(1, 3, ErrorMessage = "Please choose an event type."), Display(Name = "Event type")]
    public int? EventTypeId { get; set; }

    [Display(Name = "Banner image")]
    public IFormFile? BannerFile { get; set; }

    public string? ExistingBannerUrl { get; set; }

    // These ids match the event types seeded by the API (Run = 1, Walk = 2, Cycle = 3).
    public static IEnumerable<SelectListItem> TypeOptions => new[]
    {
        new SelectListItem("Run", "1"),
        new SelectListItem("Walk", "2"),
        new SelectListItem("Cycle", "3")
    };
}

public class CategoryFormViewModel
{
    [Required(ErrorMessage = "Please enter a category name."), StringLength(100), Display(Name = "Category name")]
    public string CategoryName { get; set; } = string.Empty;

    [Range(0, 120, ErrorMessage = "Enter an age between 0 and 120."), Display(Name = "Minimum age")]
    public int? MinAge { get; set; }

    [Range(0, 120, ErrorMessage = "Enter an age between 0 and 120."), Display(Name = "Maximum age")]
    public int? MaxAge { get; set; }

    [Range(0.01, 1000, ErrorMessage = "The distance must be between 0.01 and 1000 km."), Display(Name = "Distance (km)")]
    public decimal? DistanceKm { get; set; }
}

public class DashboardViewModel
{
    public List<EventDto> Events { get; set; } = new();
    public IEnumerable<EventDto> Upcoming => Events.Where(e => e.EventDate >= DateTime.UtcNow).OrderBy(e => e.EventDate);
    public int TotalEnrolments => Events.Sum(e => e.EnrolmentCount);
}

public class CategoriesPageViewModel
{
    public EventDto Event { get; set; } = new();
    public CategoryFormViewModel Form { get; set; } = new();
}

public class EnrolmentsPageViewModel
{
    public EventDto Event { get; set; } = new();
    public List<EnrolmentDto> Enrolments { get; set; } = new();
}

public class ResultRowViewModel
{
    public int EnrolmentId { get; set; }
    public string ParticipantName { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public string EnrolmentStatus { get; set; } = string.Empty;
    public int? ResultId { get; set; }
    public string? FinishTime { get; set; }
    public int? FinishPosition { get; set; }
    public int? TotalFinishers { get; set; }
}

public class ResultsPageViewModel
{
    public EventDto Event { get; set; } = new();
    public List<ResultRowViewModel> Rows { get; set; } = new();
    public bool EventHasPassed { get; set; }
}

public class ResultFormViewModel
{
    public int EventId { get; set; }
    public int EnrolmentId { get; set; }
    public int? ResultId { get; set; }
    public string? FinishTime { get; set; }
    public int? FinishPosition { get; set; }
    public int? TotalFinishers { get; set; }
}
