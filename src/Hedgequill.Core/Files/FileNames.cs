namespace Hedgequill.Core.Files;

/// <summary>Turns a title into a file or folder name that Windows and macOS both accept.</summary>
public static class FileNames
{
    private const int MaxLength = 80;

    private static readonly string[] Devices =
        ["CON", "PRN", "AUX", "NUL", .. Enumerable.Range(1, 9).SelectMany(n => new[] { $"COM{n}", $"LPT{n}" })];

    /// <summary>The name a title gets on disk, without an extension. It can differ from the title, which the index then keeps.</summary>
    public static string From(string title, string fallback)
    {
        var kept = new string([.. title.Where(c => c >= ' ' && !"<>:\"/\\|?*".Contains(c))]);
        // Windows drops trailing dots and spaces, and a leading dot hides the file on macOS.
        var name = kept.Trim().TrimStart('.').Trim();
        if (name.Length > MaxLength)
        {
            name = name[..MaxLength].Trim();
        }

        name = name.TrimEnd('.', ' ');
        if (name.Length == 0)
        {
            return fallback;
        }

        return Devices.Contains(name.Split('.')[0], StringComparer.OrdinalIgnoreCase) ? name + "_" : name;
    }

    /// <summary>The name, or the name with " 2", " 3" and so on, whichever isn't taken. Windows and macOS ignore case in names, so this does too.</summary>
    public static string Unique(string name, string extension, Func<string, bool> taken)
    {
        var candidate = name + extension;
        for (var n = 2; taken(candidate); n++)
        {
            candidate = $"{name} {n}{extension}";
        }

        return candidate;
    }
}
