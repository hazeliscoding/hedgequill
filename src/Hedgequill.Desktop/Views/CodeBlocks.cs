using Avalonia;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;
using Hedgequill.Core.Text;
using CaretPositioningMode = AvaloniaEdit.Document.CaretPositioningMode;
using LogicalDirection = AvaloniaEdit.Document.LogicalDirection;

namespace Hedgequill.Desktop.Views;

/// <summary>
/// A fenced code block as the prototype draws it: one rounded background behind its fences and code, and the text inset an
/// em inside it. The inset is space the editor draws, not spaces in the file.
/// </summary>
public sealed class CodeBlocks(PageBlocks blocks) : VisualLineElementGenerator, IBackgroundRenderer
{
    public IBrush Background { get; set; } = Brushes.LightGray;

    public KnownLayer Layer => KnownLayer.Background;

    public override int GetFirstInterestedOffset(int startOffset)
    {
        var line = CurrentContext.VisualLine.FirstDocumentLine;
        return startOffset == line.Offset && IsCode(line.LineNumber) ? startOffset : -1;
    }

    public override VisualLineElement ConstructElement(int offset) => new Inset(CurrentContext.GlobalTextRunProperties.FontRenderingEmSize * MarkdownStyles.CodeScale);

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        var top = (double?)null;
        var bottom = 0.0;
        foreach (var line in textView.VisualLines)
        {
            if (IsCode(line.FirstDocumentLine.LineNumber))
            {
                top ??= line.VisualTop - textView.ScrollOffset.Y;
                bottom = line.VisualTop + line.Height - textView.ScrollOffset.Y;
                if (IsLast(line.FirstDocumentLine))
                {
                    Fill(textView, drawingContext, top.Value, bottom);
                    top = null;
                }
            }
        }

        if (top is { } open)
        {
            Fill(textView, drawingContext, open, bottom);
        }
    }

    private bool IsCode(int line) => blocks.KindOf(line) is LineKind.Fence or LineKind.Code;

    private bool IsLast(DocumentLine line) => line.NextLine is not { } next || !IsCode(next.LineNumber);

    private void Fill(TextView textView, DrawingContext drawingContext, double top, double bottom) =>
        drawingContext.DrawRectangle(Background, null, new Rect(0, top, textView.Bounds.Width, bottom - top), 6, 6);

    // An em of space before a code line's text. It holds no text, so the caret skips it.
    private sealed class Inset(double width) : VisualLineElement(1, 0)
    {
        public override TextRun CreateTextRun(int startVisualColumn, ITextRunConstructionContext context) => new Space(TextRunProperties, width);

        public override int GetVisualColumn(int relativeTextOffset) => VisualColumn + 1;

        public override bool HandlesLineBorders => true;

        public override int GetNextCaretPosition(int visualColumn, LogicalDirection direction, CaretPositioningMode mode) =>
            direction == LogicalDirection.Forward
                ? visualColumn < VisualColumn + 1 ? VisualColumn + 1 : -1
                : visualColumn > VisualColumn + 1 ? VisualColumn + 1 : -1;
    }

    private sealed class Space(TextRunProperties properties, double width) : DrawableTextRun
    {
        public override TextRunProperties Properties { get; } = properties;

        public override int Length => 1;

        public override Size Size { get; } = new(width, 0);

        public override double Baseline => 0;

        public override void Draw(DrawingContext drawingContext, Point origin)
        {
        }
    }
}
