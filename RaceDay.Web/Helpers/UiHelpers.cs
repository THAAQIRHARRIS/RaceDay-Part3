using System.Globalization;

namespace RaceDay.Web.Helpers;

/// <summary>Small formatting helpers shared by the views.</summary>
public static class UiHelpers
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>CSS class for the colour-coded enrolment status badge (green / amber / red).</summary>
    public static string StatusClass(string? status) => status switch
    {
        "Confirmed" => "status-confirmed",
        "Cancelled" => "status-cancelled",
        _ => "status-pending"
    };

    /// <summary>1 -> 1st, 2 -> 2nd, 3 -> 3rd, 11 -> 11th, 47 -> 47th ...</summary>
    public static string Ordinal(int n)
    {
        var suffix = (Math.Abs(n) % 100) is >= 11 and <= 13
            ? "th"
            : (Math.Abs(n) % 10) switch { 1 => "st", 2 => "nd", 3 => "rd", _ => "th" };
        return $"{n}{suffix}";
    }

    /// <summary>"47th out of 312 finishers"</summary>
    public static string PositionText(int? position, int? total)
    {
        if (position == null) return "-";
        return total == null
            ? Ordinal(position.Value)
            : $"{Ordinal(position.Value)} out of {total} finishers";
    }

    public static string TypeIcon(string? type) => type switch
    {
        "Run" => "🏃",
        "Walk" => "🚶",
        "Cycle" => "🚴",
        _ => "🏁"
    };

    public static string FormatDate(DateTime d) => d.ToString("ddd, dd MMM yyyy", Inv);
    public static string FormatDateTime(DateTime d) => d.ToString("ddd, dd MMM yyyy 'at' HH:mm", Inv);
    public static string ShortDate(DateTime d) => d.ToString("dd MMM yyyy", Inv);
    public static string Km(decimal d) => d.ToString("0.##", Inv) + " km";

    public static string AgeRange(int? min, int? max) => (min, max) switch
    {
        (null, null) => "All ages",
        (int a, null) => $"{a}+",
        (null, int b) => $"Up to {b}",
        (int a, int b) => $"{a} to {b}"
    };

    public static string Initials(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "?";
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 1
            ? parts[0][..1].ToUpperInvariant()
            : (parts[0][..1] + parts[^1][..1]).ToUpperInvariant();
    }
}
