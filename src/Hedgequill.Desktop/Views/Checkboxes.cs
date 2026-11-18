using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;
using Hedgequill.Core.Text;

namespace Hedgequill.Desktop.Views;

/// <summary>
/// A checklist item's box, drawn as the prototype draws it. A click ticks or clears it in the file, as one step Undo takes
/// back, and the file keeps its plain [ ] or [x].
/// </summary>
public sealed class Checkboxes(PageBlocks blocks) : VisualLineElementGenerator
{
    public IBrush Border { get; set; } = Brushes.Gray;

    public IBrush Accent { get; set; } = Brushes.DarkRed;

    public IBrush Tick { get; set; } = Brushes.White;

    public override int GetFirstInterestedOffset(int startOffset)
    {
        var box = BoxAt(startOffset);
        return box is { } at && startOffset <= at.Offset ? at.Offset : -1;
    }

    public override VisualLineElement? ConstructElement(int offset) =>
        BoxAt(offset) is { } box && box.Offset == offset ? new Box(CurrentContext.Document, offset, box.Done, this) : null;

    // The box on the line holding this offset, when that line is a plain Markdown checklist item.
    private (int Offset, bool Done)? BoxAt(int offset)
    {
        var line = CurrentContext.Document.GetLineByOffset(offset);
        return blocks.KindOf(line.LineNumber) == LineKind.Text && Markdown.ListItemAt(CurrentContext.Document.GetText(line)) is { Checked: { } done } item
            ? (line.Offset + item.Indent + item.Marker, done)
            : null;
    }

    // "[ ]" or "[x]": three characters, one caret stop.
    private sealed class Box(TextDocument document, int offset, bool done, Checkboxes owner) : VisualLineElement(1, 3)
    {
        public override TextRun CreateTextRun(int startVisualColumn, ITextRunConstructionContext context) => new BoxRun(TextRunProperties, done, owner);

        protected override void OnQueryCursor(PointerEventArgs e)
        {
            if (e.Source is InputElement view)
            {
                view.Cursor = new Cursor(StandardCursorType.Hand);
                e.Handled = true;
            }
        }

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            if (e.GetCurrentPoint(null).Properties.IsLeftButtonPressed)
            {
                document.Replace(offset + 1, 1, done ? " " : "x");
                e.Handled = true;
            }
        }
    }

    // The prototype's box: 0.82 em square, 3 px corners, a 1.5 px border, filled with the accent and ticked when done.
    private sealed class BoxRun : DrawableTextRun
    {
        private readonly bool _done;
        private readonly Checkboxes _owner;
        private readonly double _side;

        public BoxRun(TextRunProperties properties, bool done, Checkboxes owner)
        {
            Properties = properties;
            _done = done;
            _owner = owner;
            var em = properties.FontRenderingEmSize;
            var metrics = properties.Typeface.GlyphTypeface.Metrics;
            var scale = em / metrics.DesignEmHeight;
            _side = em * 0.82;
            Baseline = -metrics.Ascent * scale;
            Size = new Size(_side + (em * 0.48), (metrics.Descent - metrics.Ascent) * scale);
        }

        public override TextRunProperties Properties { get; }

        public override int Length => 1;

        public override Size Size { get; }

        public override double Baseline { get; }

        public override void Draw(DrawingContext drawingContext, Point origin)
        {
            var em = Properties.FontRenderingEmSize;
            var box = new Rect(origin.X, origin.Y + Baseline + (em * 0.08) - _side, _side, _side).Deflate(0.75);
            drawingContext.DrawRectangle(_done ? _owner.Accent : null, new Pen(_done ? _owner.Accent : _owner.Border, 1.5), box, 3, 3);
            if (_done)
            {
                var tick = new StreamGeometry();
                using (var context = tick.Open())
                {
                    context.BeginFigure(Point(2.6, 6.3), false);
                    context.LineTo(Point(4.8, 8.5));
                    context.LineTo(Point(9.4, 3.6));
                    context.EndFigure(false);
                }

                drawingContext.DrawGeometry(null, new Pen(_owner.Tick, 1.8, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round), tick);
            }

            // The tick is drawn on a 12-unit square, as the prototype's is.
            Point Point(double x, double y) => new(box.X + (x * box.Width / 12), box.Y + (y * box.Height / 12));
        }
    }
}
