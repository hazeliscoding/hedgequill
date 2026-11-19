using Avalonia.Media;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;
using Hedgequill.Core.Text;

namespace Hedgequill.Desktop.Views;

/// <summary>
/// Styles Markdown in place, as the prototype's editor does: emphasis in italic, strong in bold, headings in the title
/// face at 1.3 times the size, quotes in italic and the secondary color, struck text struck through, code in the code
/// face, a ticked item's text and a comment to yourself quieter, a comment in italic too, and the markers that make them
/// faded, a link's brackets and target among them.
/// </summary>
public sealed class MarkdownStyles : DocumentColorizingTransformer
{
    public required PageBlocks Blocks { get; init; }

    public required IBrush Faded { get; set; }

    public required IBrush Quote { get; set; }

    public required IBrush Done { get; set; }

    /// <summary>Behind inline code; a fenced block's background is drawn whole, by <see cref="CodeBlocks"/>.</summary>
    public required IBrush CodeBackground { get; set; }

    public required FontFamily Title { get; set; }

    public required FontFamily Mono { get; set; }

    /// <summary>Code is set smaller than the text around it, as the prototype sets it, at 0.85 of its size.</summary>
    public const double CodeScale = 0.85;

    protected override void ColorizeLine(DocumentLine line)
    {
        var text = CurrentContext.Document.GetText(line);
        var kind = Blocks.KindOf(line.LineNumber);
        foreach (var span in Markdown.Spans(text, kind))
        {
            if (span.Length > 0)
            {
                ChangeLinePart(line.Offset + span.Start, line.Offset + span.Start + span.Length, element => Apply(element.TextRunProperties, span.Mark, kind));
            }
        }
    }

    private void Apply(VisualLineElementTextRunProperties run, Mark mark, LineKind kind)
    {
        var face = run.Typeface;
        switch (mark)
        {
            case Mark.Heading:
                run.SetTypeface(new Typeface(Title, face.Style, face.Weight));
                run.SetFontRenderingEmSize(run.FontRenderingEmSize * 1.3);
                break;
            case Mark.Quote:
                run.SetTypeface(new Typeface(face.FontFamily, FontStyle.Italic, face.Weight));
                run.SetForegroundBrush(Quote);
                break;
            case Mark.Code:
                run.SetTypeface(new Typeface(Mono, face.Style, face.Weight));
                run.SetFontRenderingEmSize(run.FontRenderingEmSize * CodeScale);
                if (kind == LineKind.Text)
                {
                    run.SetBackgroundBrush(CodeBackground);
                }

                break;
            case Mark.Emphasis:
                run.SetTypeface(new Typeface(face.FontFamily, FontStyle.Italic, face.Weight));
                break;
            case Mark.Strong:
                run.SetTypeface(new Typeface(face.FontFamily, face.Style, FontWeight.Bold));
                break;
            case Mark.Strike:
                run.SetTextDecorations(TextDecorations.Strikethrough);
                break;
            case Mark.Comment:
                run.SetTypeface(new Typeface(face.FontFamily, FontStyle.Italic, face.Weight));
                run.SetForegroundBrush(Done);
                break;
            case Mark.Done:
                run.SetForegroundBrush(Done);
                break;
            case Mark.Marker:
                run.SetForegroundBrush(Faded);
                break;
        }
    }
}
