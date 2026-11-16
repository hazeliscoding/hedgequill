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
/// the rest of the line keeps theirs. Only lines on screen are checked, a sentence at a time, and each sentence once per session.
/// </summary>
public sealed class SpellingUnderlines : IBackgroundRenderer
{
    private const int CacheLimit = 5000;
    private static readonly TimeSpan Pause = TimeSpan.FromMilliseconds(400);
    private static readonly TimeSpan Budget = TimeSpan.FromMilliseconds(4);

    private readonly TextView _view;
    private readonly SpellChecker _checker;
    private readonly Func<string, bool> _known;

    // Results by sentence, so an edit re-asks the checker about one sentence and the rest of the line comes from here.
    private readonly Dictionary<string, IReadOnlyList<Misspelling>> _cache = [];

    // The lines checked or queued since their last edit, each with the generation of its queued sentences.
    private readonly Dictionary<DocumentLine, int> _checked = [];
    private readonly Queue<(DocumentLine Line, int From, int Generation)> _work = new();
    private readonly DispatcherTimer _timer;
    private int _generation;
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

    /// <summary>
    /// Checks the lines on screen that changed or haven't been checked, as the pause after typing does. A sentence at a time
    /// for a few milliseconds, then the rest after the next frame, so a keystroke never waits long behind it.
    /// </summary>
    public void CheckVisible()
    {
        _timer.Stop();
        if (_document is null || !_view.VisualLinesValid)
        {
            return;
        }

        foreach (var line in _view.VisualLines.Select(visual => visual.FirstDocumentLine))
        {
            if (!line.IsDeleted && !_checked.ContainsKey(line))
            {
                _checked[line] = ++_generation;
                foreach (var old in _found.FindOverlappingSegments(line.Offset, line.Length).ToList())
                {
                    _found.Remove(old);
                }

                _work.Enqueue((line, 0, _generation));
            }
        }

        foreach (var line in _checked.Keys.Where(line => line.IsDeleted).ToList())
        {
            _checked.Remove(line);
        }

        Continue();
    }

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        if (_document is null || !textView.VisualLinesValid || textView.VisualLines.Count == 0)
        {
            return;
        }

        var lines = textView.VisualLines;
        if (!_timer.IsEnabled && lines.Any(visual => !_checked.ContainsKey(visual.FirstDocumentLine)))
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

    private void Continue()
    {
        var clock = Stopwatch.StartNew();
        var changed = false;
        while (_document is not null && _work.Count > 0 && clock.Elapsed < Budget)
        {
            // A line edited since it was queued waits for the next pause; its generation tells.
            var (line, from, generation) = _work.Dequeue();
            if (line.IsDeleted || _checked.GetValueOrDefault(line) != generation)
            {
                continue;
            }

            var text = _document.GetText(line);
            var to = SentenceEnd(text, from);
            var sentence = text[from..to];
            if (!_cache.TryGetValue(sentence, out var words))
            {
                if (_cache.Count >= CacheLimit)
                {
                    _cache.Clear();
                }

                _cache[sentence] = words = _checker.Check(sentence);
            }

            foreach (var word in words)
            {
                _found.Add(new TextSegment { StartOffset = line.Offset + from + word.Start, Length = word.Length });
            }

            if (to < text.Length)
            {
                _work.Enqueue((line, to, generation));
            }

            changed = true;
        }

        if (changed)
        {
            _view.InvalidateLayer(Layer);
        }

        if (_work.Count > 0)
        {
            Dispatcher.UIThread.Post(Continue, DispatcherPriority.Background);
        }
    }

    // Where the sentence starting at the offset ends: after the space that follows . ! or ?, or at a space within 300 characters.
    private static int SentenceEnd(string text, int from)
    {
        var limit = Math.Min(text.Length, from + 300);
        for (var i = from; i < limit - 1; i++)
        {
            if (text[i] is '.' or '!' or '?' && text[i + 1] == ' ')
            {
                return i + 2;
            }
        }

        if (limit == text.Length)
        {
            return limit;
        }

        var space = text.LastIndexOf(' ', limit - 1, limit - from);
        return space > from ? space + 1 : limit;
    }

    private void Attach(TextDocument? document)
    {
        if (_document is not null)
        {
            _document.Changing -= OnChanging;
            _document.Changed -= OnChanged;
        }

        _document = document;
        _checked.Clear();
        _work.Clear();
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
