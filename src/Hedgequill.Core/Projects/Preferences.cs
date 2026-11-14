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
    private string FilePath => Path.Combine(settingsFolder, "settings.json");

    /// <summary>View › Appearance. A missing or damaged file means Match system.</summary>
    public Appearance Appearance
    {
        get
        {
            try
            {
                return JsonSerializer.Deserialize(File.ReadAllText(FilePath), CoreJson.Readable.PreferencesFile)?.Appearance ?? Appearance.System;
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
            {
                return Appearance.System;
            }
        }

        set
        {
            Directory.CreateDirectory(settingsFolder);
            AtomicFile.Write(FilePath, JsonSerializer.Serialize(new PreferencesFile { Appearance = value }, CoreJson.Readable.PreferencesFile) + "\n");
        }
    }
}

internal sealed class PreferencesFile
{
    public Appearance Appearance { get; set; }
}
