using System.Diagnostics;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Hedgequill.Desktop.Themes;
using Hedgequill.Desktop.Views;
using Hedgequill.Tests.Core;
using static Hedgequill.Tests.Desktop.Ui;

namespace Hedgequill.Tests.Desktop;

/// <summary>The prototype's motion, and none of it when the system asks for reduced motion.</summary>
public sealed class MotionTests : IDisposable
{
    private readonly TempFolder _temp = new();

    public MotionTests() => _temp.Write("stories/Tides/Chapter 1/Fog.md", "Invented fog rolled in.");

    [Fact]
    public Task With_motion_off_nothing_fades_and_the_navigator_goes_at_once() => OnProject(workspace =>
    {
        Assert.Null(Presenter(Find<Button>(workspace, "NavigatorToggle")).Transitions);
        workspace.ToggleNavigator();
        Assert.Equal(0, Navigator(workspace).Width.Value);
        Assert.False(Find<Panel>(workspace, "NavigatorPane").IsVisible);
    });

    [Fact]
    public Task With_motion_on_the_panes_slide_shut_and_open() => OnProject(workspace =>
    {
        Motion.On = true;
        try
        {
            // Each motion runs twice and is checked the second time: real time drives it, and a first run can stall on JIT.
            workspace.ToggleNavigator();
            Assert.Equal(0, Frames(() => Navigator(workspace).Width.Value)[^1]);
            Assert.False(Find<Panel>(workspace, "NavigatorPane").IsVisible);

            workspace.ToggleNavigator();
            var opening = Frames(() => Navigator(workspace).Width.Value);
            Assert.Contains(opening, width => width is > 0 and < 236);
            Assert.Equal(236, opening[^1]);
            Assert.True(Find<Panel>(workspace, "NavigatorPane").IsVisible);

            var preview = Find<PreviewView>(workspace, "Preview");
            workspace.TogglePreview();
            Assert.Equal(0, Frames(() => Column(workspace, 3).Width.Value)[^1]);
            Assert.False(preview.IsVisible);

            workspace.TogglePreview();
            var showing = Frames(() => Column(workspace, 3).Width.Value);
            Assert.Contains(showing, width => width is > 0 and < 340);
            Assert.Equal(340, showing[^1]);
            // Landed, the preview fills its column again, so the splitter can resize it.
            Assert.True(double.IsNaN(preview.Width));
        }
        finally
        {
            Motion.On = false;
        }
    });

    [Fact]
    public Task With_motion_on_a_chevron_turns_and_the_save_details_pop_in() => OnProject(workspace =>
    {
        Motion.On = true;
        try
        {
            var chapter = workspace.Rows.Single(row => row.IsChapter).Node;
            workspace.Toggle(chapter);
            Frames(() => 0);
            workspace.Toggle(chapter);
            var chevron = Find<ListBox>(workspace, "Navigator").ContainerFromIndex(0)!.GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Path>().First();
            var angles = Frames(() => ((RotateTransform)chevron.RenderTransform!).Angle);
            Assert.Contains(angles, angle => angle is > 0 and < 90);
            Assert.Equal(90, angles[^1]);

            var status = workspace.GetVisualDescendants().OfType<SaveStatus>().Single();
            var details = (Flyout)Find<Button>(status, "Label").Flyout!;
            status.ShowDetails();
            Frames(() => 0);
            details.Hide();
            status.ShowDetails();
            var popover = ((Visual)details.Content!).FindAncestorOfType<FlyoutPresenter>()!;
            var opacities = Frames(() => popover.Opacity);
            Assert.Contains(opacities, opacity => opacity is > 0 and < 1);
            Assert.Equal(1, opacities[^1]);
            details.Hide();
        }
        finally
        {
            Motion.On = false;
        }
    });

    [Fact]
    public Task The_motion_styles_use_the_prototypes_timings() => OnProject(workspace =>
    {
        var styles = new MotionStyles();
        Avalonia.Application.Current!.Styles.Add(styles);
        try
        {
            Settle();
            var hover = Assert.IsType<BrushTransition>(Assert.Single(Presenter(Find<Button>(workspace, "NavigatorToggle")).Transitions!));
            Assert.Equal(TimeSpan.FromMilliseconds(120), hover.Duration);
            Assert.IsType<SplineEasing>(hover.Easing);
            var fade = Assert.IsType<DoubleTransition>(Assert.Single(Find<Grid>(workspace, "Header").Transitions!));
            Assert.Equal(TimeSpan.FromMilliseconds(180), fade.Duration);
        }
        finally
        {
            Avalonia.Application.Current!.Styles.Remove(styles);
        }
    });

    public void Dispose() => _temp.Dispose();

    private static ColumnDefinition Navigator(WorkspaceView workspace) => Column(workspace, 0);

    private static ColumnDefinition Column(WorkspaceView workspace, int index) => Find<Grid>(workspace, "Columns").ColumnDefinitions[index];

    private static ContentPresenter Presenter(Button button) =>
        button.GetVisualDescendants().OfType<ContentPresenter>().First(presenter => presenter.Name == "PART_ContentPresenter");

    // A value on each frame, for a little longer than the 260 ms slide.
    private static List<double> Frames(Func<double> read)
    {
        var values = new List<double>();
        var clock = Stopwatch.StartNew();
        while (clock.ElapsedMilliseconds < 500)
        {
            Thread.Sleep(16);
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Settle();
            values.Add(read());
        }

        return values;
    }

    private Task OnProject(Action<WorkspaceView> test) => OnWindow(ThemeVariant.Light, _temp["settings"], window =>
    {
        Assert.True(window.Open(_temp["stories/Tides"]));
        Settle();
        test(window.Workspace!);
    });
}
