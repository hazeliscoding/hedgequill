using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hedgequill.Core.Projects;

/// <summary>The contents of hedgequill.json.</summary>
internal sealed class ProjectIndex
{
    public const int CurrentFormat = 1;

    public int Format { get; set; } = CurrentFormat;

    /// <summary>Each folder's contents in order, keyed by its path from the project folder; "" is the project itself.</summary>
    public Dictionary<string, List<string>>? Order { get; set; }

    /// <summary>The top-level folders that are acts, so an empty one doesn't reopen as a chapter.</summary>
    public List<string>? Acts { get; set; }

    /// <summary>Titles that differ from their file or folder name, keyed by path.</summary>
    public Dictionary<string, string>? Titles { get; set; }
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    PropertyNameCaseInsensitive = true,
    AllowTrailingCommas = true,
    ReadCommentHandling = JsonCommentHandling.Skip)]
[JsonSerializable(typeof(ProjectIndex))]
[JsonSerializable(typeof(List<RecentProject>))]
internal sealed partial class CoreJson : JsonSerializerContext
{
    /// <summary>Writes "é" and "’" as themselves, and LF line endings on every platform, so the files read well in any editor.</summary>
    /// <remarks>Built on first use: static initializers in two halves of a partial class run in no set order, so Default may not exist yet.</remarks>
    public static CoreJson Readable => field ??= new(new JsonSerializerOptions(Default.Options) { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, NewLine = "\n" });
}
