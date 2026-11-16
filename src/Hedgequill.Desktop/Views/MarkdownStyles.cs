using Avalonia.Media;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;
using Hedgequill.Core.Text;

namespace Hedgequill.Desktop.Views;

/// <summary>
/// Styles Markdown in place, as the prototype's editor does: emphasis in italic, strong in bold, headings in the title
/// face at 1.3 times the size, quotes in italic and the secondary color, struck text struck through, and the markers that
/// make them faded, a link's brackets and target among them.
/// </summary>
public sealed class MarkdownStyles : DocumentColorizingTransformer
{
    public required IBrush Faded { get; set; }

    public required IBrush Quote { get; set; }

    public required FontFamily Title { get; set; }

    protected override void ColorizeLine(DocumentLine line)
    {
        var text = CurrentContext.Document.GetText(line);
        foreach (var span in Markdown.Spans(text))
        {
            if (span.Length > 0)
            {
                ChangeLinePart(line.Offset + span.Start, line.Offset + span.Start + span.Length, element => Apply(element.TextRunProperties, span.Mark));
            }
        }
    }

    private void Apply(VisualLineElementTextRunProperties run, Mark mark)
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
            case Mark.Emphasis:
                run.SetTypeface(new Typeface(face.FontFamily, FontStyle.Italic, face.Weight));
                break;
            case Mark.Strong:
                run.SetTypeface(new Typeface(face.FontFamily, face.Style, FontWeight.Bold));
                break;
            case Mark.Strike:
                run.SetTextDecorations(TextDecorations.Strikethrough);
                break;
            case Mark.Marker:
                run.SetForegroundBrush(Faded);
                break;
        }
    }
}
