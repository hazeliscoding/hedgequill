using Avalonia.Input;
using Avalonia.Styling;
using AvaloniaEdit;
using AvaloniaEdit.Rendering;
using Hedgequill.Core.Projects;
using Hedgequill.Desktop.Views;
using Hedgequill.Tests.Core;
using static Hedgequill.Tests.Desktop.Ui;

namespace Hedgequill.Tests.Desktop;

/// <summary>View › Larger text, Smaller text and Actual size: the page's text only, remembered in settings.json.</summary>
public sealed class TextSizeTests : IDisposable
{
    private readonly TempFolder _temp = new();

    public TextSizeTests()
    {
        _temp.Write("stories/Tides/Chapter 1/Fog.md", "Invented fog rolled in.");
        _temp.Write("stories/Tides/Notes/Cast.md", "An invented ferryman.");
    }

    private static RawInputModifiers Command => OperatingSystem.IsMacOS() ? RawInputModifiers.Meta : RawInputModifiers.Control;

    [Fact]
    public Task The_shortcuts_scale_the_page_and_render_its_lines_taller() => OnProject((window, workspace) =>
    {
        var editor = workspace.Editor;
        using (Render(window))
        {
            Assert.Equal(19, editor.FontSize);
        }

        var normal = Row(editor);

        Press(window, PhysicalKey.Equal, Command);
        Press(window, PhysicalKey.Equal, Command);
        using (Render(window))
        {
            Assert.Equal(19 * 1.25, editor.FontSize, 3);
            Assert.Equal(normal * 1.25, Row(editor), 0.5);
        }

        Assert.Equal("Text size 125%", Find<Toast>(window, "Notice").Title);
        Assert.Equal(125, new Preferences(_temp["settings"]).TextSize);
        // Only the text scales; the scene's title keeps the prototype's size.
        Assert.Equal(30, Find<Avalonia.Controls.TextBlock>(workspace, "DocTitle").FontSize);

        Press(window, PhysicalKey.Minus, Command);
        Assert.Equal(19 * 1.1, editor.FontSize, 3);
        Press(window, PhysicalKey.Digit0, Command);
        Assert.Equal(19, editor.FontSize);
    });

    [Fact]
    public Task A_remembered_size_applies_to_pages_too() => OnProject(
        (window, workspace) =>
        {
            Assert.Equal(19 * 1.6, workspace.Editor.FontSize, 3);
            workspace.Select(workspace.Project.Root.Descendants().Single(node => node.Kind == NodeKind.Page));
            Settle();
            Assert.Equal(15.5 * 1.6, workspace.Editor.FontSize, 3);
        },
        before: () => new Preferences(_temp["settings"]).TextSize = 160);

    // The first line's first row of text, below the room the scene's title rides in.
    private static double Row(TextEditor editor)
    {
        var view = editor.TextArea.TextView;
        var start = new TextViewPosition(1, 1);
        return view.GetVisualPosition(start, VisualYPosition.LineBottom).Y - view.GetVisualPosition(start, VisualYPosition.LineTop).Y;
    }

    public void Dispose() => _temp.Dispose();

    private Task OnProject(Action<MainWindow, WorkspaceView> test, Action? before = null)
    {
        before?.Invoke();
        return OnWindow(ThemeVariant.Light, _temp["settings"], window =>
        {
            Assert.True(window.Open(_temp["stories/Tides"]));
            Settle();
            test(window, window.Workspace!);
        });
    }
}
