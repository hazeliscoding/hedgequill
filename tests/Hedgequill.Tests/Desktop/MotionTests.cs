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
            // Each motion runs once before it's checked, since a first run can stall on JIT.
            double Width() => Navigator(workspace).Width.Value;
            workspace.ToggleNavigator();
            Assert.Equal(0, Until(Width, 0)[^1]);
            Assert.False(Find<Panel>(workspace, "NavigatorPane").IsVisible);

            var opening = Animated(workspace.ToggleNavigator, () => Shut(workspace.ToggleNavigator, Width), Width, 0, 236);
            Assert.Contains(opening, width => width is > 0 and < 236);
            Assert.Equal(236, opening[^1]);
            Assert.True(Find<Panel>(workspace, "NavigatorPane").IsVisible);

            var preview = Find<PreviewView>(workspace, "Preview");
            double PreviewWidth() => Column(workspace, 3).Width.Value;
            workspace.TogglePreview();
            Assert.Equal(0, Until(PreviewWidth, 0)[^1]);
            Assert.False(preview.IsVisible);

            var showing = Animated(workspace.TogglePreview, () => Shut(workspace.TogglePreview, PreviewWidth), PreviewWidth, 0, 340);
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
            // These are Avalonia keyframe animations, whose frames wait for the compositor. On the macOS runner the headless
            // render loop sometimes stops partway (PR #14's probe froze 8 turns in 40), so this checks that each one moves,
            // not where it lands. The slides above are driven frame by frame and don't wait for it.
            var chapter = workspace.Rows.Single(row => row.IsChapter).Node;
            double Angle() => ((RotateTransform)Find<ListBox>(workspace, "Navigator").ContainerFromIndex(0)!
                .GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Path>().First().RenderTransform!).Angle;
            workspace.Toggle(chapter);
            Until(Angle, 0);
            var angles = Animated(() => workspace.Toggle(chapter), () => Shut(() => workspace.Toggle(chapter), Angle), Angle, 0, 90);
            Assert.Contains(angles, angle => angle is > 0 and < 90);

            var status = workspace.GetVisualDescendants().OfType<SaveStatus>().Single();
            var details = (Flyout)Find<Button>(status, "Label").Flyout!;
            double Opacity() => ((Visual)details.Content!).FindAncestorOfType<FlyoutPresenter>()?.Opacity ?? 0;
            status.ShowDetails();
            Until(Opacity, 1);
            details.Hide();
            var opacities = Animated(status.ShowDetails, details.Hide, Opacity, 0, 1);
            Assert.Contains(opacities, opacity => opacity is > 0 and < 1);
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

    /// <summary>
    /// Plays a motion and returns its frames, once one has landed between its two ends. Real time drives the motion, and
    /// a busy runner can stall through all of it, so it gets five tries, with <paramref name="undo"/> between them. A motion
    /// that doesn't animate at all fails all five.
    /// </summary>
    private static List<double> Animated(Action play, Action undo, Func<double> read, double from, double to)
    {
        for (var attempt = 1; ; attempt++)
        {
            play();
            var frames = Until(read, to);
            if (attempt == 5 || frames.Any(value => value > Math.Min(from, to) && value < Math.Max(from, to)))
            {
                return frames;
            }

            undo();
        }
    }

    private static void Shut(Action toggle, Func<double> read)
    {
        toggle();
        Until(read, 0);
    }

    // A value on each frame until it has held its end for a tenth of a second, or two seconds have passed.
    private static List<double> Until(Func<double> read, double end)
    {
        var values = new List<double>();
        var clock = Stopwatch.StartNew();
        var held = new Stopwatch();
        while (clock.ElapsedMilliseconds < 2000 && held.ElapsedMilliseconds < 100)
        {
            Thread.Sleep(16);
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Settle();
            var value = read();
            values.Add(value);
            if (value != end)
            {
                held.Reset();
            }
            else if (!held.IsRunning)
            {
                held.Start();
            }
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
