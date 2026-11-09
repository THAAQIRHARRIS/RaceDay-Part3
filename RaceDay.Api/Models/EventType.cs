using System.ComponentModel.DataAnnotations;

namespace RaceDay.Api.Models;

public class EventType
{
    public int EventTypeId { get; set; }
    [Required, MaxLength(20)] public string TypeName { get; set; } = string.Empty;

    public ICollection<Event> Events { get; set; } = new List<Event>();
}
