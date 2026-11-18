using System.Globalization;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;
using Hedgequill.Core.Text;

namespace Hedgequill.Desktop.Views;

/// <summary>
/// Lines a table's columns up on screen, as the owner chose: a pipe after a narrower cell is drawn further right, by as many
/// code-face characters as the cell is short of its column's widest. The file keeps the table exactly as typed.
/// </summary>
public sealed class TableColumns(PageBlocks blocks) : VisualLineElementGenerator
{
    public override int GetFirstInterestedOffset(int startOffset)
    {
        var line = CurrentContext.Document.GetLineByOffset(startOffset);
        foreach (var (pipe, _) in Padded(line))
        {
            if (line.Offset + pipe >= startOffset)
            {
                return line.Offset + pipe;
            }
        }

        return -1;
    }

    public override VisualLineElement? ConstructElement(int offset)
    {
        var line = CurrentContext.Document.GetLineByOffset(offset);
        return Padded(line).FirstOrDefault(pair => line.Offset + pair.Pipe == offset) is { Pad: > 0 } found ? new Pipe(found.Pad) : null;
    }

    // Each pipe after the first, with how many characters short the cell before it is.
    private IEnumerable<(int Pipe, int Pad)> Padded(DocumentLine line)
    {
        if (blocks.KindOf(line.LineNumber) is not (LineKind.TableHeader or LineKind.TableRule or LineKind.TableRow))
        {
            yield break;
        }

        var columns = blocks.ColumnsOf(line.LineNumber);
        var pipes = Markdown.Pipes(CurrentContext.Document.GetText(line));
        for (var i = 1; i < pipes.Count && i - 1 < columns.Length; i++)
        {
            var pad = columns[i - 1] - (pipes[i] - pipes[i - 1] - 1);
            if (pad > 0)
            {
                yield return (pipes[i], pad);
            }
        }
    }

    private sealed class Pipe(int pad) : VisualLineElement(1, 1)
    {
        public override TextRun CreateTextRun(int startVisualColumn, ITextRunConstructionContext context) => new PipeRun(TextRunProperties, pad);
    }

    // The pipe at the right of its padding, so the caret before it sits right after the cell's text.
    private sealed class PipeRun : DrawableTextRun
    {
        private readonly FormattedText _pipe;
        private readonly double _pad;

        public PipeRun(TextRunProperties properties, int pad)
        {
            Properties = properties;
            _pipe = Text("|");
            _pad = Text("0").WidthIncludingTrailingWhitespace * pad;
            Size = new Size(_pad + _pipe.WidthIncludingTrailingWhitespace, _pipe.Height);

            FormattedText Text(string text) => new(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, properties.Typeface, properties.FontRenderingEmSize, properties.ForegroundBrush);
        }

        public override TextRunProperties Properties { get; }

        public override int Length => 1;

        public override Size Size { get; }

        public override double Baseline => _pipe.Baseline;

        public override void Draw(DrawingContext drawingContext, Point origin) => drawingContext.DrawText(_pipe, new Point(origin.X + _pad, origin.Y));
    }
}
