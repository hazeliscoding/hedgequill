using System.Diagnostics;
using Avalonia;
using Avalonia.Media;
using Avalonia.Threading;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;

namespace Hedgequill.Desktop.Spelling;

/// <summary>
/// Underlines misspelled words with a thin wavy line, as the prototype draws it. A line is checked once the text has stood
/// still for a moment, so a keystroke never waits on the checker; the word being typed loses its underline until then, and
/// the rest of the line keeps theirs. Only lines on screen are checked, and a line's text is checked once per session.
/// </summary>
public sealed class SpellingUnderlines : IBackgroundRenderer
{
    private const int CacheLimit = 5000;
    private static readonly TimeSpan Pause = TimeSpan.FromMilliseconds(400);
    private static readonly TimeSpan Budget = TimeSpan.FromMilliseconds(8);

    private readonly TextView _view;
    private readonly SpellChecker _checker;
    private readonly Func<string, bool> _known;
    private readonly Dictionary<string, IReadOnlyList<Misspelling>> _cache = [];
    private readonly HashSet<DocumentLine> _checked = [];
    private readonly DispatcherTimer _timer;
    private TextSegmentCollection<TextSegment> _found = [];
    private TextDocument? _document;

    public SpellingUnderlines(TextView view, SpellChecker checker, Func<string, bool> known)
    {
        _view = view;
        _checker = checker;
        _known = known;
        _timer = new DispatcherTimer(Pause, DispatcherPriority.Background, (_, _) => CheckVisible());
        view.DocumentChanged += (_, _) => Attach(view.Document);
        Attach(view.Document);
    }

    public KnownLayer Layer => KnownLayer.Selection;

    public IBrush Brush { get; set; } = Brushes.Red;

    /// <summary>The underlined word at a document offset, if there's one.</summary>
    public TextSegment? At(int offset) => _found.FindSegmentsContaining(offset).FirstOrDefault(segment => !IsKnown(segment));

    /// <summary>Redraws, leaving out words the project dictionary has taken since.</summary>
    public void Refresh() => _view.InvalidateLayer(Layer);

    /// <summary>Checks the lines on screen now, as the pause after typing would.</summary>
    public void CheckVisible()
    {
        _timer.Stop();
        if (_document is null || !_view.VisualLinesValid)
        {
            return;
        }

        var clock = Stopwatch.StartNew();
        var changed = false;
        foreach (var line in _view.VisualLines.Select(visual => visual.FirstDocumentLine))
        {
            if (line.IsDeleted || _checked.Contains(line))
            {
                continue;
            }

            // A long first look at a chapter carries on after the next frame instead of holding it up.
            if (clock.Elapsed > Budget)
            {
                Dispatcher.UIThread.Post(CheckVisible, DispatcherPriority.Background);
                break;
            }

            _checked.Add(line);
            var text = _document.GetText(line);
            if (!_cache.TryGetValue(text, out var words))
            {
                if (_cache.Count >= CacheLimit)
                {
                    _cache.Clear();
                }

                _cache[text] = words = _checker.Check(text);
            }

            foreach (var old in _found.FindOverlappingSegments(line.Offset, line.Length).ToList())
            {
                _found.Remove(old);
            }

            foreach (var word in words)
            {
                _found.Add(new TextSegment { StartOffset = line.Offset + word.Start, Length = word.Length });
            }

            changed = true;
        }

        _checked.RemoveWhere(line => line.IsDeleted);
        if (changed)
        {
            _view.InvalidateLayer(Layer);
        }
    }

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        if (_document is null || !textView.VisualLinesValid || textView.VisualLines.Count == 0)
        {
            return;
        }

        var lines = textView.VisualLines;
        if (!_timer.IsEnabled && lines.Any(visual => !_checked.Contains(visual.FirstDocumentLine)))
        {
            _timer.Start();
        }

        var start = lines[0].FirstDocumentLine.Offset;
        var end = lines[^1].LastDocumentLine.EndOffset;
        var pen = new Pen(Brush, 1);
        foreach (var word in _found.FindOverlappingSegments(start, end - start))
        {
            if (IsKnown(word) || textView.GetVisualLine(_document.GetLineByOffset(word.StartOffset).LineNumber) is not { } visual)
            {
                continue;
            }

            foreach (var box in BackgroundGeometryBuilder.GetRectsForSegment(textView, word))
            {
                drawingContext.DrawGeometry(null, pen, Wave(box.Left, box.Right, Baseline(textView, visual, box) + 3.5));
            }
        }
    }

    private bool IsKnown(TextSegment word) => _document is not null && _known(_document.GetText(word));

    private void Attach(TextDocument? document)
    {
        if (_document is not null)
        {
            _document.Changing -= OnChanging;
            _document.Changed -= OnChanged;
        }

        _document = document;
        _checked.Clear();
        _found = document is null ? [] : new TextSegmentCollection<TextSegment>(document);
        if (document is not null)
        {
            document.Changing += OnChanging;
            document.Changed += OnChanged;
        }

        _timer.Stop();
        _timer.Start();
    }

    // Before the edit, in its own offsets: the word it touches loses its underline.
    private void OnChanging(object? sender, DocumentChangeEventArgs e)
    {
        foreach (var word in _found.FindOverlappingSegments(Math.Max(e.Offset - 1, 0), e.RemovalLength + 2).ToList())
        {
            _found.Remove(word);
        }
    }

    // After it: every line it touched waits for the pause, then gets checked again.
    private void OnChanged(object? sender, DocumentChangeEventArgs e)
    {
        var last = _document!.GetLineByOffset(Math.Min(e.Offset + e.InsertionLength, _document.TextLength));
        for (var line = _document.GetLineByOffset(e.Offset); line is not null; line = line.NextLine)
        {
            _checked.Remove(line);
            if (line == last)
            {
                break;
            }
        }

        _timer.Stop();
        _timer.Start();
    }

    // The baseline of the row the box sits on; the box spans the row's whole line height.
    private static double Baseline(TextView view, VisualLine visual, Rect box)
    {
        var scroll = view.ScrollOffset.Y;
        foreach (var row in visual.TextLines)
        {
            if (Math.Abs(visual.GetTextLineVisualYPosition(row, VisualYPosition.LineTop) - scroll - box.Top) < 1)
            {
                return visual.GetTextLineVisualYPosition(row, VisualYPosition.Baseline) - scroll;
            }
        }

        return box.Bottom - 6;
    }

    // A zigzag a pixel either side of the line, two pixels per step, like CSS's wavy underline.
    private static StreamGeometry Wave(double left, double right, double y)
    {
        var geometry = new StreamGeometry();
        using var context = geometry.Open();
        context.BeginFigure(new Point(left, y), false);
        for (var (x, up) = (left, -1.0); x < right; up = -up)
        {
            x = Math.Min(x + 2, right);
            context.LineTo(new Point(x, y + up));
        }

        context.EndFigure(false);
        return geometry;
    }
}
