using System.Text.Json;
using Hedgequill.Core.Files;

namespace Hedgequill.Core.Projects;

public sealed record RecentProject(string Folder, string Title, DateTimeOffset Opened, int Words);

/// <summary>The last few projects opened, newest first, in the settings folder.</summary>
public sealed class RecentProjects(string settingsFolder)
{
    public const int Limit = 8;

    private string FilePath => Path.Combine(settingsFolder, "recent.json");

    /// <summary>%LOCALAPPDATA%\Hedgequill on Windows, ~/Library/Application Support/Hedgequill on macOS.</summary>
    public static string DefaultFolder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Hedgequill");

    public IReadOnlyList<RecentProject> Load()
    {
        try
        {
            return JsonSerializer.Deserialize(File.ReadAllText(FilePath), CoreJson.Readable.ListRecentProject) ?? [];
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
        {
            // A missing or damaged list starts empty; the projects themselves are untouched.
            return [];
        }
    }

    public void Add(Project project, DateTimeOffset opened)
    {
        var entries = Load()
            .Where(entry => !string.Equals(entry.Folder, project.Folder, StringComparison.OrdinalIgnoreCase))
            .Prepend(new RecentProject(project.Folder, project.Title, opened, project.Words))
            .Take(Limit)
            .ToList();
        Directory.CreateDirectory(settingsFolder);
        AtomicFile.Write(FilePath, JsonSerializer.Serialize(entries, CoreJson.Readable.ListRecentProject) + "\n");
    }
}
