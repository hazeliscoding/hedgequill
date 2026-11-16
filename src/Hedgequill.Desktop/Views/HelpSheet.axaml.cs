using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Hedgequill.Desktop.Updates;

namespace Hedgequill.Desktop.Views;

public enum HelpPage
{
    Writing,
    Shortcuts,
    Ao3,
    About,
}

/// <summary>A heading in Keyboard shortcuts and the commands under it.</summary>
public sealed record ShortcutGroup(string Title, IReadOnlyList<ShortcutRow> Rows)
{
    /// <summary>The prototype sets group titles in capitals; screen readers get <see cref="Title"/>.</summary>
    public string Caps => Title.ToUpperInvariant();
}

/// <summary>A command and its keys as the menu shows them: Ctrl+B on Windows, ⌘B on a Mac.</summary>
public sealed record ShortcutRow(string Label, string Keys)
{
    public string Spoken => $"{Label}, {Keys}";
}

public partial class HelpSheet : UserControl
{
    private static readonly Dictionary<HelpPage, string> Titles = new()
    {
        [HelpPage.Writing] = "Writing in Hedgequill",
        [HelpPage.Shortcuts] = "Keyboard shortcuts",
        [HelpPage.Ao3] = "Pasting into AO3",
        [HelpPage.About] = "About Hedgequill",
    };

    private Control? _returnFocus;

    public HelpSheet()
    {
        InitializeComponent();
        AboutVersion.Text = $"Version {Updater.Version}";
        Done.Click += (_, _) => Hide();
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

    public void Show(HelpPage page, IReadOnlyList<ShortcutGroup> shortcuts)
    {
        Heading.Text = Titles[page];
        AutomationProperties.SetName(this, Heading.Text);
        Card.Width = page == HelpPage.About ? 420 : 560;
        WritingPage.IsVisible = page == HelpPage.Writing;
        ShortcutsPage.IsVisible = page == HelpPage.Shortcuts;
        ShortcutsPage.ItemsSource = shortcuts;
        Ao3Page.IsVisible = page == HelpPage.Ao3;
        AboutPage.IsVisible = page == HelpPage.About;
        Pages.Offset = default;
        _returnFocus = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() as Control;
        IsVisible = true;
        Dispatcher.UIThread.Post(() => Done.Focus(NavigationMethod.Tab), DispatcherPriority.Loaded);
    }

    public void Hide()
    {
        IsVisible = false;
        _returnFocus?.Focus();
    }
}
