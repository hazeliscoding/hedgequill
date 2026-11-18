using System.Globalization;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Rendering;
using Hedgequill.Core.Text;

namespace Hedgequill.Desktop.Views;

/// <summary>
/// Shows a scene break as the prototype's ornament: its marks centred in the column, faded and half an em apart. The file
/// keeps the plain Markdown break. The line holding the caret shows that plain text instead, so it can be edited, as
/// Obsidian's Live Preview does; Bold on an empty line types "****", which is a break until the first letter goes in.
/// </summary>
public sealed class SceneBreaks : VisualLineElementGenerator
{
    private readonly TextArea _area;
    private readonly PageBlocks _blocks;
    private int _caretLine;
    private bool _shown;

    public SceneBreaks(TextArea area, PageBlocks blocks)
    {
        _area = area;
        _blocks = blocks;
        _caretLine = area.Caret.Line;
        area.Caret.PositionChanged += (_, _) =>
        {
            var line = area.Caret.Line;
            if (line != _caretLine)
            {
                Redraw(_caretLine);
                Redraw(line);
                _caretLine = line;
            }
        };

        // An ornament is centred in the width the text view had when it was built, which lags a resize by one layout.
        area.TextView.SizeChanged += (_, e) =>
        {
            if (e.WidthChanged && _shown)
            {
                _shown = false;
                area.TextView.Redraw();
            }
        };
    }

    public override int GetFirstInterestedOffset(int startOffset)
    {
        var line = CurrentContext.VisualLine.FirstDocumentLine;
        return startOffset == line.Offset && line.LineNumber != _area.Caret.Line && IsBreak(line.LineNumber, CurrentContext.Document.GetText(line))
            ? line.Offset
            : -1;
    }

    public override VisualLineElement ConstructElement(int offset)
    {
        var line = CurrentContext.Document.GetLineByOffset(offset);
        var marks = CurrentContext.Document.GetText(line).Trim();
        _shown = true;
        return new Ornament(line.Length, marks[0], marks.Count(c => c == marks[0]), CurrentContext.TextView.Bounds.Width);
    }

    private void Redraw(int lineNumber)
    {
        var document = _area.Document;
        if (document is not null && lineNumber <= document.LineCount && document.GetLineByNumber(lineNumber) is var line && IsBreak(lineNumber, document.GetText(line)))
        {
            _area.TextView.Redraw(line);
        }
    }

    // Three dashes inside a code block or under a table's header are code and a rule row, not a break.
    private bool IsBreak(int lineNumber, string text) => _blocks.KindOf(lineNumber) == LineKind.Text && Markdown.IsSceneBreak(text);

    // It never holds the caret, since the caret's line shows plain text.
    private sealed class Ornament(int length, char mark, int count, double column) : VisualLineElement(1, length)
    {
        public override TextRun CreateTextRun(int startVisualColumn, ITextRunConstructionContext context) => new Run(TextRunProperties, mark, count, column);
    }

    private sealed class Run : DrawableTextRun
    {
        private readonly FormattedText _mark;
        private readonly int _count;
        private readonly double _step;
        private readonly double _marks;

        public Run(TextRunProperties properties, char mark, int count, double column)
        {
            Properties = properties;
            _mark = new FormattedText(mark.ToString(), CultureInfo.CurrentCulture, FlowDirection.LeftToRight, properties.Typeface, properties.FontRenderingEmSize, properties.ForegroundBrush);
            _count = count;
            _step = _mark.WidthIncludingTrailingWhitespace + (properties.FontRenderingEmSize / 2);
            _marks = (_step * count) - (properties.FontRenderingEmSize / 2);

            // The run ends where the marks do, so the caret after the break sits beside it.
            Size = new Size(Math.Max(_marks, (column + _marks) / 2), _mark.Height);
        }

        public override TextRunProperties Properties { get; }

        public override int Length => 1;

        public override Size Size { get; }

        public override double Baseline => _mark.Baseline;

        public override void Draw(DrawingContext drawingContext, Point origin)
        {
            var x = origin.X + Size.Width - _marks;
            for (var i = 0; i < _count; i++, x += _step)
            {
                drawingContext.DrawText(_mark, new Point(x, origin.Y));
            }
        }
    }
}
