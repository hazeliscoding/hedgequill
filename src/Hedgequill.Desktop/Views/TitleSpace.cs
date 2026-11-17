using Avalonia;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using AvaloniaEdit.Rendering;
using CaretPositioningMode = AvaloniaEdit.Document.CaretPositioningMode;
using LogicalDirection = AvaloniaEdit.Document.LogicalDirection;

namespace Hedgequill.Desktop.Views;

/// <summary>
/// The room above a scene's first line where its title rides. It's inside the text, at the start of the first line and
/// ended by a line break, so it scrolls with the text: as the editor's top padding it stayed put, and AvaloniaEdit drew
/// nothing in it, so the first lines vanished into the gap under the title once the scene scrolled. It holds no
/// characters, so the file never sees it, and the caret never stops in it.
/// </summary>
public sealed class TitleSpace(TextView view) : VisualLineElementGenerator
{
    private double _height;

    public double Height
    {
        get => _height;
        set
        {
            if (Math.Abs(_height - value) < 0.01)
            {
                return;
            }

            _height = value;
            if (view.Document is { LineCount: > 0 } document)
            {
                view.Redraw(document.GetLineByNumber(1));
            }
        }
    }

    public override int GetFirstInterestedOffset(int startOffset) => startOffset == 0 && _height > 0 ? 0 : -1;

    public override VisualLineElement ConstructElement(int offset) => new Room(_height, CurrentContext.VisualLine.FirstDocumentLine.Length == 0);

    // The room, then the break that starts the text on the next row. AvaloniaEdit makes no row past a line's last column,
    // so an empty first line gets a zero-width character there for the caret to sit on.
    private sealed class Room(double height, bool empty) : VisualLineElement(empty ? 3 : 2, 0)
    {
        private int Text => VisualColumn + 2;

        public override TextRun CreateTextRun(int startVisualColumn, ITextRunConstructionContext context) => (startVisualColumn - VisualColumn) switch
        {
            0 => new Gap(TextRunProperties, context.GlobalTextRunProperties, height),
            1 => new TextEndOfLine(1),
            _ => new TextCharacters("\u200B", TextRunProperties),
        };

        public override int GetVisualColumn(int relativeTextOffset) => Text;

        // The line's start is where the text begins, past the room.
        public override bool HandlesLineBorders => true;

        public override int GetNextCaretPosition(int visualColumn, LogicalDirection direction, CaretPositioningMode mode) => direction == LogicalDirection.Forward
            ? visualColumn < Text ? Text : -1
            : visualColumn > Text ? Text : -1;
    }

    private sealed class Gap : DrawableTextRun
    {
        public Gap(TextRunProperties properties, TextRunProperties paragraph, double height)
        {
            Properties = properties;
            // The row adds the paragraph font's descent and line gap below the run's baseline, so they come out of the height.
            var metrics = paragraph.Typeface.GlyphTypeface.Metrics;
            var scale = paragraph.FontRenderingEmSize / metrics.DesignEmHeight;
            var gap = metrics.LineGap * scale;
            Size = new Size(0, Math.Max(0, height - gap));
            Baseline = Math.Max(0, Size.Height - (metrics.Descent * scale));
        }

        public override TextRunProperties Properties { get; }

        public override int Length => 1;

        public override Size Size { get; }

        public override double Baseline { get; }

        public override void Draw(DrawingContext drawingContext, Point origin)
        {
        }
    }
}
