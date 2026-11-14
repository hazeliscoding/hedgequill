using System.Globalization;
using Hedgequill.Core.Projects;
using Hedgequill.Core.Text;

namespace Hedgequill.Desktop.Views;

/// <summary>One project in the Welcome screen's Recent list, as the prototype lays it out.</summary>
public sealed record RecentRow(string Folder, string Title, string Location, string When, string Words)
{
    public string Initial => Title.Length > 0 ? Title[..1].ToUpper(CultureInfo.InvariantCulture) : "";

    public static RecentRow From(RecentProject entry, DateTimeOffset now) =>
        new(entry.Folder, entry.Title, Paths.Display(entry.Folder), Labels.When(entry.Opened.ToLocalTime(), now), Labels.Words(entry.Words));
}
