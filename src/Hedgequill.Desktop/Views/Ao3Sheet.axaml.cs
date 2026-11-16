using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Hedgequill.Core.Text;

namespace Hedgequill.Desktop.Views;

/// <summary>A chapter copied for AO3: its number, title, HTML and counts, whether the clipboard took it, and any warnings.</summary>
public sealed record ChapterCopy(int Number, string Title, string Html, int Scenes, int Words, bool Copied, IReadOnlyList<string> Warnings)
{
    /// <summary>AO3's limit on a chapter, in characters, as its posting form shows it.</summary>
    public const int Limit = 500_000;
}

public partial class Ao3Sheet : UserControl
{
    private ChapterCopy? _copy;
    private Func<string, Task<bool>>? _copyText;
    private Action<string>? _copied;
    private Control? _returnFocus;

    public Ao3Sheet()
    {
        InitializeComponent();
        Done.Click += (_, _) => Hide();
        CopyAgain.Click += (_, _) => _ = Copy(_copy?.Html, "Copied again");
        CopyTitle.Click += (_, _) => _ = Copy(_copy?.Title, "Chapter title copied");
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                Hide();
                e.Handled = true;
            }
        };
    }

    public string Title => Heading.Text ?? "";

    /// <summary>Opens on a copied chapter. Copying again goes through the workspace, which tells the writer it worked.</summary>
    public void Show(ChapterCopy copy, Func<string, Task<bool>> copyText, Action<string> copied)
    {
        (_copy, _copyText, _copied) = (copy, copyText, copied);
        Heading.Text = copy.Copied ? $"Chapter {copy.Number} copied for AO3" : $"Chapter {copy.Number} as AO3 HTML";
        AutomationProperties.SetName(this, Heading.Text);
        ChapterTitle.Text = copy.Title;
        Stats.Text = $"{copy.Scenes} scene{(copy.Scenes == 1 ? "" : "s")} joined with <hr /> · {Labels.Words(copy.Words)}";
        Html.Text = copy.Html;
        Warnings.IsVisible = copy.Warnings.Count > 0;
        WarningTitle.Text = copy.Warnings.Count == 1 ? "1 thing to fix before posting" : $"{copy.Warnings.Count} things to fix before posting";
        WarningList.ItemsSource = copy.Warnings;
        _returnFocus = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() as Control;
        IsVisible = true;
        Dispatcher.UIThread.Post(() => (copy.Copied ? Done : CopyAgain).Focus(NavigationMethod.Tab), DispatcherPriority.Loaded);
    }

    public void Hide()
    {
        IsVisible = false;
        _returnFocus?.Focus();
    }

    private async Task Copy(string? text, string done)
    {
        if (text is not null && _copyText is not null && await _copyText(text))
        {
            _copied?.Invoke(done);
        }
    }
}
