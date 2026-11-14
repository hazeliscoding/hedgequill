using System.Globalization;

namespace Hedgequill.Core.Text;

/// <summary>Dates and counts as the prototype writes them. The app's copy is English.</summary>
public static class Labels
{
    private static readonly CultureInfo English = CultureInfo.InvariantCulture;

    /// <summary>"Today, 00:12", "Yesterday", "Last Tuesday", "3 Oct" or "3 Oct 2025". Pass both times in local time.</summary>
    public static string When(DateTimeOffset when, DateTimeOffset now) => (now.Date - when.Date).Days switch
    {
        0 => $"Today, {when.ToString("HH:mm", English)}",
        1 => "Yesterday",
        > 1 and < 7 => $"Last {when.DayOfWeek}",
        _ => when.ToString(when.Year == now.Year ? "d MMM" : "d MMM yyyy", English),
    };

    public static string Words(int count) => count == 1 ? "1 word" : $"{count.ToString("N0", English)} words";
}
