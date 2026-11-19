using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;
using Hedgequill.Core.Text;

namespace Hedgequill.Desktop.Views;

/// <summary>
/// The editor's lines whose look takes their neighbours to tell, fenced code and tables, with each table's column widths.
/// Worked out again after every change, and the text view is redrawn when a change moves a fence or a table or resizes a
/// column, since lines far from the change can look different then.
/// </summary>
public sealed class PageBlocks
{
    private readonly TextView _view;
    private TextDocument? _document;
    private LineKind[] _kinds = [];
    private int[][] _columns = [];

    public PageBlocks(TextView view)
    {
        _view = view;
        view.DocumentChanged += (_, _) => Attach(view.Document);
        Attach(view.Document);
    }

    /// <summary>What a line is, by its number from 1.</summary>
    public LineKind KindOf(int line) => line - 1 < _kinds.Length ? _kinds[line - 1] : LineKind.Text;

    /// <summary>The column widths of the table a line is in, in characters, or none.</summary>
    public int[] ColumnsOf(int line) => line - 1 < _columns.Length ? _columns[line - 1] : [];

    private void Attach(TextDocument? document)
    {
        if (_document is not null)
        {
            _document.Changed -= OnChanged;
        }

        _document = document;
        if (document is not null)
        {
            document.Changed += OnChanged;
        }

        Update();
    }

    private void OnChanged(object? sender, DocumentChangeEventArgs e)
    {
        var (kinds, columns) = (_kinds, _columns);
        Update();
        if (Moved(kinds, _kinds) || !columns.Zip(_columns).All(pair => pair.First.SequenceEqual(pair.Second)))
        {
            _view.Redraw();
        }
    }

    // Only a line starting with a backtick, tilde or pipe can be a fence or a table row, and only one holding <!-- or --> can
    // open or close a comment, so only those are read; the rest count as blank, which gives them the same kind. A chapter
    // with none of them, the usual case, allocates nothing per keystroke.
    private void Update()
    {
        var document = _document;
        if (document is null || !document.Lines.Any(line => MaybeBlock(document, line)))
        {
            (_kinds, _columns) = ([], []);
            return;
        }

        var lines = document.Lines.Select(line => MaybeBlock(document, line) ? document.GetText(line) : "").ToArray();
        _kinds = Markdown.Kinds(lines);
        _columns = new int[lines.Length][];
        for (var first = 0; first < lines.Length;)
        {
            var last = first;
            if (_kinds[first] == LineKind.TableHeader)
            {
                while (last + 1 < lines.Length && _kinds[last + 1] is LineKind.TableRule or LineKind.TableRow)
                {
                    last++;
                }

                var widths = Markdown.Columns(lines[first..(last + 1)]);
                Array.Fill(_columns, widths, first, last - first + 1);
            }
            else
            {
                _columns[first] = [];
            }

            first = last + 1;
        }
    }

    private static bool MaybeBlock(TextDocument document, DocumentLine line)
    {
        for (var offset = line.Offset; offset < line.EndOffset; offset++)
        {
            var c = document.GetCharAt(offset);
            if (c is not (' ' or '\t'))
            {
                return c is '`' or '~' or '|' || document.IndexOf("<!--", line.Offset, line.Length, StringComparison.Ordinal) >= 0
                    || document.IndexOf("-->", line.Offset, line.Length, StringComparison.Ordinal) >= 0;
            }
        }

        return false;
    }

    // Plain text only shifts when lines come and go, which the text view already handles; code and tables need a full redraw.
    private static bool Moved(LineKind[] before, LineKind[] after) =>
        (before.Any(kind => kind != LineKind.Text) || after.Any(kind => kind != LineKind.Text)) && !before.SequenceEqual(after);
}
