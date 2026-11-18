using System.Text.Json;
using Hedgequill.Core.Files;

namespace Hedgequill.Core.Projects;

public enum Appearance
{
    System,
    Light,
    Dark,
}

/// <summary>Choices that outlive a session, in settings.json beside the Recent list.</summary>
public sealed class Preferences(string settingsFolder)
{
    /// <summary>View › Larger text and Smaller text step through these sizes, in percent of the prototype's.</summary>
    public static readonly int[] TextSizes = [80, 90, 100, 110, 125, 140, 160, 180, 200];

    private string FilePath => Path.Combine(settingsFolder, "settings.json");

    /// <summary>View › Appearance. A missing or damaged file means Match system.</summary>
    public Appearance Appearance
    {
        get => Read().Appearance;
        set => Write(file => file.Appearance = value);
    }

    /// <summary>The page's text size in percent, one of <see cref="TextSizes"/>. A missing or damaged file means 100.</summary>
    public int TextSize
    {
        get => Nearest(Read().TextSize);
        set => Write(file => file.TextSize = Nearest(value));
    }

    /// <summary>The pages that open in reading view, by their full paths. A missing or damaged file means none.</summary>
    public IReadOnlySet<string> ReadingPages => Read().ReadingPages.ToHashSet(StringComparer.Ordinal);

    /// <summary>Remembers whether a page opens in reading view.</summary>
    public void SetReading(string page, bool reading) => Write(file =>
    {
        file.ReadingPages.Remove(page);
        if (reading)
        {
            file.ReadingPages.Add(page);
        }
    });

    /// <summary>The next size up (1), down (-1), or back to 100 (0).</summary>
    public static int Step(int size, int direction) =>
        direction == 0 ? 100 : TextSizes[Math.Clamp(Array.IndexOf(TextSizes, Nearest(size)) + Math.Sign(direction), 0, TextSizes.Length - 1)];

    private static int Nearest(int size) => TextSizes.MinBy(step => Math.Abs(step - size));

    private PreferencesFile Read()
    {
        try
        {
            return JsonSerializer.Deserialize(File.ReadAllText(FilePath), CoreJson.Readable.PreferencesFile) ?? new();
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
        {
            return new();
        }
    }

    private void Write(Action<PreferencesFile> change)
    {
        var file = Read();
        change(file);
        Directory.CreateDirectory(settingsFolder);
        AtomicFile.Write(FilePath, JsonSerializer.Serialize(file, CoreJson.Readable.PreferencesFile) + "\n");
    }
}

internal sealed class PreferencesFile
{
    public Appearance Appearance { get; set; }

    public int TextSize { get; set; } = 100;

    public List<string> ReadingPages { get; set; } = [];
}
