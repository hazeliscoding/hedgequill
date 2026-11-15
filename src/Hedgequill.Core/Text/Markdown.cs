namespace Hedgequill.Core.Text;

/// <summary>One replacement in a text, and where the caret goes after it, so an editor can apply it as one undoable step.</summary>
public readonly record struct Change(int Offset, int Length, string Insert, int Caret)
{
    public string ApplyTo(string text) => string.Concat(text.AsSpan(0, Offset), Insert, text.AsSpan(Offset + Length));
}

/// <summary>The plain Markdown markers Bold, Italic and Insert scene break add, as the prototype does.</summary>
public static partial class Markdown
{
    public const string Bold = "**";
    public const string Italic = "*";
    public const string SceneBreak = "---";

    /// <summary>
    /// Wraps the selection in the marker, or unwraps it when it's already wrapped, with or without its markers selected.
    /// With nothing selected, the caret lands between a new pair.
    /// </summary>
    public static Change Wrap(string text, int start, int end, string marker)
    {
        var inner = text[start..end];
        if (inner.Length >= marker.Length * 2 && inner.StartsWith(marker, StringComparison.Ordinal) && inner.EndsWith(marker, StringComparison.Ordinal))
        {
            return new Change(start, inner.Length, inner[marker.Length..^marker.Length], start);
        }

        if (IsStyled(text, start, end, marker))
        {
            return new Change(start - marker.Length, inner.Length + (marker.Length * 2), inner, start - marker.Length);
        }

        return new Change(start, inner.Length, marker + inner + marker, start + marker.Length + inner.Length);
    }

    // Whether the selection is exactly what this marker styles, as the editor shows it: "ran" in "**ran**" for Bold.
    private static bool IsStyled(string text, int start, int end, string marker)
    {
        var lineStart = text.LastIndexOf('\n', Math.Max(start - 1, 0)) + 1;
        var lineEnd = text.IndexOf('\n', start) is var newline and >= 0 ? newline : text.Length;
        var mark = marker == Bold ? Mark.Strong : Mark.Emphasis;
        return start > lineStart && end <= lineEnd
            && Spans(text[lineStart..lineEnd]).Any(span => span.Mark == mark && lineStart + span.Start == start && span.Length == end - start)
            && text.AsSpan(start - marker.Length, marker.Length).SequenceEqual(marker) && text.AsSpan(end, marker.Length).SequenceEqual(marker);
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
