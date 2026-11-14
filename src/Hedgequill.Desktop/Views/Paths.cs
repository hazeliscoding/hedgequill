namespace Hedgequill.Desktop.Views;

internal static class Paths
{
    private static readonly string Home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    /// <summary>A folder as people read it: "~/Stories/Small Hours" on macOS, the full path on Windows, where "~" isn't a habit.</summary>
    public static string Display(string folder) =>
        !OperatingSystem.IsWindows() && Home.Length > 0 && folder.StartsWith(Home, StringComparison.Ordinal) ? "~" + folder[Home.Length..] : folder;
}
