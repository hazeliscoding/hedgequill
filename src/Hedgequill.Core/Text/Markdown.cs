namespace Hedgequill.Core.Text;

/// <summary>One replacement in a text, and where the caret goes after it, so an editor can apply it as one undoable step.</summary>
public readonly record struct Change(int Offset, int Length, string Insert, int Caret)
{
    public string ApplyTo(string text) => string.Concat(text.AsSpan(0, Offset), Insert, text.AsSpan(Offset + Length));
}

/// <summary>The plain Markdown markers Bold, Italic and Insert scene break add, as the prototype does.</summary>
public static class Markdown
{
    public const string Bold = "**";
    public const string Italic = "*";
    public const string SceneBreak = "---";

    /// <summary>Wraps the selection in the marker, or unwraps it when it's already wrapped. With nothing selected, the caret lands between a new pair.</summary>
    public static Change Wrap(string text, int start, int end, string marker)
    {
        var inner = text[start..end];
        if (inner.Length >= marker.Length * 2 && inner.StartsWith(marker, StringComparison.Ordinal) && inner.EndsWith(marker, StringComparison.Ordinal))
        {
            return new Change(start, inner.Length, inner[marker.Length..^marker.Length], start);
        }

        return new Change(start, inner.Length, marker + inner + marker, start + marker.Length + inner.Length);
    }

    /// <summary>Replaces the selection with a scene break on a line of its own, a blank line on each side, and the caret after it.</summary>
    public static Change InsertSceneBreak(string text, int start, int end)
    {
        var from = text.AsSpan(0, start).TrimEnd("\r\n").Length;
        var to = text.Length - text.AsSpan(end).TrimStart("\r\n").Length;
        var insert = (from > 0 ? "\n\n" : "") + SceneBreak + "\n\n";
        return new Change(from, to - from, insert, from + insert.Length);
    }
}
