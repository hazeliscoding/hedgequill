using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;

namespace Hedgequill.Desktop.Themes;

/// <summary>The prototype's motion, from `docs/brand/tokens.md`: its durations and curves, and whether anything moves at all.</summary>
public static class Motion
{
    public static readonly TimeSpan Base = TimeSpan.FromMilliseconds(180);
    public static readonly TimeSpan Slow = TimeSpan.FromMilliseconds(260);

    public static readonly Easing EaseOut = new SplineEasing(0.22, 1, 0.36, 1);

    /// <summary>The soft spring: it overshoots by a few percent and settles.</summary>
    public static readonly Easing Spring = new SplineEasing(0.3, 1.12, 0.5, 1);

    /// <summary>Off unless Program turns it on, so a system that asks for reduced motion, and the tests, see every change at once.</summary>
    public static bool On { get; set; }

    /// <summary>Opens a popover as the prototype's save details do: fading in from 96% and 6 px lower, anchored at its top right.</summary>
    public static void PopIn(Visual popover)
    {
        if (!On)
        {
            return;
        }

        popover.RenderTransformOrigin = new RelativePoint(1, 0, RelativeUnit.Relative);
        _ = Between(popover, Base, Spring, (Visual.OpacityProperty, 0.0, 1.0), (ScaleTransform.ScaleXProperty, 0.96, 1.0), (ScaleTransform.ScaleYProperty, 0.96, 1.0), (TranslateTransform.YProperty, 6.0, 0.0));
    }

    /// <summary>Turns a navigator chevron, whose render transform is a rotation, from one angle to the other.</summary>
    public static void Turn(Visual chevron, double from, double to)
    {
        if (On)
        {
            _ = Between(chevron, Base, EaseOut, (RotateTransform.AngleProperty, from, to));
        }
    }

    // Avalonia animates a transform through its parts, such as RotateTransform.Angle, not as a whole RenderTransform.
    private static Task Between(Visual target, TimeSpan duration, Easing easing, params (AvaloniaProperty Property, object From, object To)[] values)
    {
        var start = new KeyFrame { Cue = new Cue(0) };
        var end = new KeyFrame { Cue = new Cue(1) };
        foreach (var (property, from, to) in values)
        {
            start.Setters.Add(new Setter(property, from));
            end.Setters.Add(new Setter(property, to));
        }

        return new Animation { Duration = duration, Easing = easing, Children = { start, end } }.RunAsync(target);
    }
}

/// <summary>
/// Moves a value to a new target over 260 ms on the soft spring, a frame at a time, as the prototype's panes and page slide.
/// Grid columns can't take a transition, so this drives them by hand. A new target takes over from a slide still running.
/// </summary>
public sealed class Slide(Action<double> apply)
{
    private int _run;

    /// <summary>Slides from one value to another, then calls <paramref name="landed"/>.</summary>
    public void Run(Visual visual, double from, double to, Action? landed = null)
    {
        var run = ++_run;
        // Before its first layout there's nothing on screen to move.
        if (!Motion.On || from == to || visual.Bounds.Width == 0 || TopLevel.GetTopLevel(visual) is not { } top)
        {
            apply(to);
            landed?.Invoke();
            return;
        }

        TimeSpan? start = null;
        top.RequestAnimationFrame(Step);

        void Step(TimeSpan now)
        {
            if (run != _run)
            {
                return;
            }

            start ??= now;
            var progress = Math.Min(1, (now - start.Value) / Motion.Slow);
            apply(progress < 1 ? from + ((to - from) * Motion.Spring.Ease(progress)) : to);
            if (progress < 1)
            {
                top.RequestAnimationFrame(Step);
            }
            else
            {
                landed?.Invoke();
            }
        }
    }
}
