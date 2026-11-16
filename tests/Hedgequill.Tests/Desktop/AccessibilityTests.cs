using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Hedgequill.Core.Projects;
using Hedgequill.Desktop.Views;
using Hedgequill.Tests.Core;
using static Hedgequill.Tests.Desktop.Ui;

namespace Hedgequill.Tests.Desktop;

/// <summary>
/// M5's accessibility: every control Narrator and VoiceOver can reach has a name, and every button a mouse can press takes
/// keyboard focus unless a key does its job. ContrastTests holds the 4.5:1 check.
/// </summary>
public sealed class AccessibilityTests : IDisposable
{
    // The buttons that stay out of the Tab order, and the keys that do their jobs instead.
    private static readonly Dictionary<string, string> KeyInstead = new()
    {
        ["Bold"] = "Ctrl+B or ⌘B",
        ["Italic"] = "Ctrl+I or ⌘I",
        ["Scene break"] = "Ctrl+Enter or ⌘↩",
        ["Expand or collapse"] = "Right and Left in the navigator",
    };

    private readonly TempFolder _temp = new();

    public AccessibilityTests()
    {
        _temp.Write("stories/Tides/Chapter 1/Fog.md", "Invented fog rolled in.");
        _temp.Write("stories/Tides/Notes/Mara.md", "An invented lighthouse keeper.");
    }

    [Fact]
    public Task Every_control_a_screen_reader_reaches_has_a_name() => OnWindow(ThemeVariant.Light, _temp["settings"], window =>
    {
        var unnamed = new List<string>();
        void Check(string state) => unnamed.AddRange(Unnamed(window).Select(control => $"{state}: {control}"));

        Check("Welcome");
        window.NewProject();
        Check("New project");
        Find<NewProjectSheet>(window, "Sheet").Hide();

        Assert.True(window.Open(_temp["stories/Tides"]));
        Settle();
        var workspace = window.Workspace!;
        var nodes = workspace.Project.Root.Descendants().ToList();
        workspace.Select(nodes.Single(node => node.Kind == NodeKind.Scene));
        Check("Scene");
        workspace.Select(nodes.Single(node => node.Kind == NodeKind.Chapter));
        Check("Chapter overview");
        workspace.Select(nodes.Single(node => node.Kind == NodeKind.Page));
        Check("Page");
        workspace.ShowWork();
        Check("Summary and notes");
        workspace.OpenFindInProject();
        Check("Find in project");
        workspace.CloseFindInProject();
        workspace.ShowVersions(nodes.Single(node => node.Kind == NodeKind.Scene));
        Check("Earlier versions");
        Find<VersionsSheet>(window, "Versions").Hide();
        foreach (var page in Enum.GetValues<HelpPage>())
        {
            window.ShowHelp(page);
            Check(page.ToString());
            Find<HelpSheet>(window, "Help").Hide();
        }

        workspace.ToggleFocus();
        Check("Focus mode");

        Assert.Empty(unnamed);
    });

    [Fact]
    public Task Every_button_takes_focus_unless_a_key_does_its_job() => OnWindow(ThemeVariant.Light, _temp["settings"], window =>
    {
        Assert.True(window.Open(_temp["stories/Tides"]));
        var workspace = window.Workspace!;
        workspace.Select(workspace.Project.Root.Descendants().Single(node => node.Kind == NodeKind.Scene));
        Settle();

        var mouseOnly = window.GetVisualDescendants().OfType<Button>()
            .Where(button => button.IsEffectivelyVisible && button.IsEffectivelyEnabled && !button.Focusable)
            .Select(Name)
            .Distinct()
            .ToList();
        Assert.NotEmpty(mouseOnly);
        Assert.All(mouseOnly, name => Assert.Contains(name, KeyInstead.Keys));
    });

    [Fact]
    public Task The_name_check_catches_an_icon_button_and_a_row_named_after_its_type() => OnWindow(
        ThemeVariant.Light,
        window =>
        {
            Assert.Equal(["Button", "ListBoxItem"], Unnamed(window).Select(found => found.Split(' ')[0]).Order(StringComparer.Ordinal));
        },
        () => new Window
        {
            Content = new StackPanel
            {
                Children =
                {
                    new Button { Content = new Avalonia.Controls.Shapes.Path() },
                    new Button { Content = "Named" },
                    new ListBox { Name = "Rows", ItemsSource = new[] { new ShortcutRow("Bold", "Ctrl+B") }, [Avalonia.Automation.AutomationProperties.NameProperty] = "Rows" },
                },
            },
        });

    public void Dispose() => _temp.Dispose();

    // What Narrator and VoiceOver can land on: anything that takes focus or a click, and the lists and their rows. Scroll bars
    // are left out; screen readers scroll by themselves.
    private static IEnumerable<string> Unnamed(Visual root)
    {
        Settle();
        return root.GetVisualDescendants().OfType<Control>()
            .Where(control => control.IsEffectivelyVisible && control.FindAncestorOfType<ScrollBar>(includeSelf: true) is null)
            .Where(control => control is Button or TextBox or ListBox or ListBoxItem or MenuItem or ComboBox or AvaloniaEdit.Editing.TextArea
                || (control.Focusable && KeyboardNavigation.GetIsTabStop(control) && control is not (ScrollViewer or ContentPresenter)))
            .Where(control => Name(control) is not { Length: > 0 } name || string.IsNullOrWhiteSpace(name) || ReadsAsAType(name))
            .Select(control => $"{control.GetType().Name} {control.Name} under {control.GetVisualAncestors().OfType<Control>().FirstOrDefault(parent => parent.Name is { Length: > 0 })?.Name}")
            .Distinct();
    }

    private static string? Name(Control control) => ControlAutomationPeer.CreatePeerForElement(control).GetName();

    // A row named after its item's ToString(): a class's full name, or a record's "VersionRow { … }".
    private static bool ReadsAsAType(string name) => name.StartsWith("Hedgequill.", StringComparison.Ordinal) || name.Contains(" { ", StringComparison.Ordinal);
}
