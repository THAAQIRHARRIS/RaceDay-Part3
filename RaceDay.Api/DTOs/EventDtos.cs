using System.ComponentModel.DataAnnotations;
using RaceDay.Api.Models;

namespace RaceDay.Api.DTOs;

public class EventRequest
{
    [Required, StringLength(150)] public string Name { get; set; } = string.Empty;
    [StringLength(4000)] public string? Description { get; set; }
    [Required] public DateTime? EventDate { get; set; }
    [Required, StringLength(200)] public string Location { get; set; } = string.Empty;
    [Range(0.01, 1000)] public decimal DistanceKm { get; set; }

    /// <summary>1 = Run, 2 = Walk, 3 = Cycle.</summary>
    [Range(1, int.MaxValue)] public int EventTypeId { get; set; }
}

public class EventResponse
{
    public int EventId { get; set; }
    public int OrganiserId { get; set; }
    public string OrganiserName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime EventDate { get; set; }
    public string Location { get; set; } = string.Empty;
    public decimal DistanceKm { get; set; }
    public int EventTypeId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string? BannerImageUrl { get; set; }
    public int EnrolmentCount { get; set; }

    /// <summary>Only populated by GET /api/events/{id}.</summary>
    public List<CategoryResponse>? Categories { get; set; }
}

public class CategoryRequest
{
    [Required, StringLength(100)] public string CategoryName { get; set; } = string.Empty;
    [Range(0, 120)] public int? MinAge { get; set; }
    [Range(0, 120)] public int? MaxAge { get; set; }
    [Range(0.01, 1000)] public decimal? DistanceKm { get; set; }
}

public class CategoryResponse
{
    public int CategoryId { get; set; }
    public int EventId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public int? MinAge { get; set; }
    public int? MaxAge { get; set; }
    public decimal? DistanceKm { get; set; }

    public static CategoryResponse From(Category c) => new()
    {
        CategoryId = c.CategoryId,
        EventId = c.EventId,
        CategoryName = c.CategoryName,
        MinAge = c.MinAge,
        MaxAge = c.MaxAge,
        DistanceKm = c.DistanceKm
    };
}
