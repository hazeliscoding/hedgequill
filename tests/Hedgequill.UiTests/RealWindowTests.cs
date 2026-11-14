using System.Diagnostics;
using System.Runtime.InteropServices;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Capturing;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.Tools;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;
using Hedgequill.Core.Projects;
using Point = System.Drawing.Point;

namespace Hedgequill.UiTests;

/// <summary>
/// Drives the published app's real window on Windows: the caption row's menus, typing and saving, Find, focus mode,
/// dragging the window by its caption row, and closing straight after a keystroke. It uses invented text, its own
/// settings folder through HEDGEQUILL_SETTINGS, and leaves screenshots in HEDGEQUILL_UI_SHOTS.
/// </summary>
public sealed partial class RealWindowTests : IDisposable
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(15);

    private readonly string _root = Directory.CreateTempSubdirectory("hedgequill-ui-").FullName;
    private readonly string _shots = Environment.GetEnvironmentVariable("HEDGEQUILL_UI_SHOTS") is { Length: > 0 } shots ? shots : Path.Combine(Path.GetTempPath(), "hedgequill-ui-shots");

    private static string? App => Environment.GetEnvironmentVariable("HEDGEQUILL_APP");

    [Fact]
    public void Writing_in_the_real_window_reaches_the_file()
    {
        Assert.SkipUnless(System.OperatingSystem.IsWindows() && File.Exists(App), "Set HEDGEQUILL_APP to the published Hedgequill.exe; Windows only.");

        var fog = Path.Combine(_root, "stories", "Tides", "Chapter 1", "Fog.md");
        Directory.CreateDirectory(Path.GetDirectoryName(fog)!);
        File.WriteAllText(fog, "Invented fog rolled in.");
        new RecentProjects(Path.Combine(_root, "settings")).Add(Project.Open(Path.Combine(_root, "stories", "Tides")), DateTimeOffset.Now);
        Directory.CreateDirectory(_shots);

        var start = new ProcessStartInfo(App!) { UseShellExecute = false };
        start.Environment["HEDGEQUILL_SETTINGS"] = Path.Combine(_root, "settings");
        using var app = Application.Launch(start);
        using var automation = new UIA3Automation();
        try
        {
            var window = app.GetMainWindow(automation, Patience) ?? throw new InvalidOperationException("Hedgequill showed no window.");
            // A known size and place, inside even a 1024 × 768 CI screen.
            SetWindowPos(window.Properties.NativeWindowHandle.Value, 0, 40, 40, 960, 640, 0x0004);
            window.Focus();
            Capture.Element(window).ToFile(Path.Combine(_shots, "windows-welcome.png"));

            // The caption row carries the prototype's menu bar. Windows adds the title bar's own System menu.
            Assert.Equal(
                ["File", "Edit", "View", "Chapter"],
                window.FindAllDescendants(cf => cf.ByControlType(ControlType.MenuItem)).Select(item => item.Name).Where(name => name != "System"));

            // Recent opens the project.
            Wait(() => window.FindFirstDescendant(cf => cf.ByName("Tides").And(cf.ByControlType(ControlType.Button))), "Recent's Tides").Click();
            Assert.True(Retry.WhileFalse(() => window.Title == "Tides — Hedgequill", Patience).Result, $"The title is {window.Title}.");

            // Typing in the editor reaches the file within the autosave's second. The editor fills the pane right of the
            // 236 px navigator and below the 32 px caption row and 40 px header; a click below the text puts the caret at the end.
            Point Page() => new(window.BoundingRectangle.Left + 236 + ((window.BoundingRectangle.Width - 236) / 2), window.BoundingRectangle.Top + 32 + 40 + 300);
            Mouse.Click(Page());
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.END);
            Keyboard.Type(" Typed in the real window.");
            Assert.True(
                Retry.WhileFalse(() => File.ReadAllText(fog).Contains("Typed in the real window.", StringComparison.Ordinal), Patience).Result,
                $"Fog.md holds: {File.ReadAllText(fog)}");
            Capture.Element(window).ToFile(Path.Combine(_shots, "windows-workspace.png"));

            // Ctrl+F opens AvaloniaEdit's search, a text field that wasn't there before.
            var fields = window.FindAllDescendants(cf => cf.ByControlType(ControlType.Edit)).Length;
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_F);
            Assert.True(Retry.WhileFalse(() => window.FindAllDescendants(cf => cf.ByControlType(ControlType.Edit)).Length > fields, Patience).Result, "Find didn't open.");
            Keyboard.Type(VirtualKeyShort.ESCAPE);

            // View › Focus mode, from the caption row's menu, hides the navigator; Escape in the editor brings it back.
            // Clicking View must open the menu, not start dragging the window by its caption row.
            Wait(() => window.FindFirstDescendant(cf => cf.ByName("View").And(cf.ByControlType(ControlType.MenuItem))), "the View menu").Click();
            var focus = Wait(() => InApp(app, automation, window, "Focus mode"), "View › Focus mode in an open View menu");
            Capture.Screen().ToFile(Path.Combine(_shots, "windows-view-menu.png"));
            focus.Click();
            Assert.True(Retry.WhileFalse(() => Navigator(window) is null, Patience).Result, "Focus mode left the navigator showing.");
            Keyboard.Type(VirtualKeyShort.ESCAPE);
            Assert.True(Retry.WhileFalse(() => Navigator(window) is not null, Patience).Result, "Escape didn't leave focus mode.");

            // The caption row moves the window, as a title bar does.
            var before = window.BoundingRectangle;
            var grip = new Point(before.Left + (before.Width / 2), before.Top + 16);
            Mouse.Drag(grip, new Point(grip.X + 120, grip.Y + 60));
            Assert.True(
                Retry.WhileFalse(() => window.BoundingRectangle.Left - before.Left is > 100 and < 140, Patience).Result,
                $"The window went from {before} to {window.BoundingRectangle}.");

            // Closing straight after a keystroke still saves it.
            Mouse.Click(Page());
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.END);
            Keyboard.Type(" Last words.");
            window.Close();
            Assert.True(Retry.WhileFalse(() => app.HasExited, Patience).Result, "Hedgequill didn't close.");
            Assert.EndsWith(" Last words.", File.ReadAllText(fog), StringComparison.Ordinal);
        }
        catch
        {
            Capture.Screen().ToFile(Path.Combine(_shots, "windows-failure.png"));
            if (app.GetMainWindow(automation, TimeSpan.FromSeconds(2)) is { } shown)
            {
                File.WriteAllLines(Path.Combine(_shots, "windows-uia-tree.txt"), Tree(automation, shown));
            }

            throw;
        }
        finally
        {
            if (!app.HasExited)
            {
                app.Kill();
            }
        }
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private static AutomationElement? Navigator(Window window) =>
        window.FindFirstDescendant(cf => cf.ByName("Manuscript").And(cf.ByControlType(ControlType.List)));

    /// <summary>The window's raw UI Automation tree, one element a line, for working out what a failed search missed.</summary>
    private static IEnumerable<string> Tree(UIA3Automation automation, AutomationElement root)
    {
        var walker = automation.TreeWalkerFactory.GetRawViewWalker();
        var lines = new List<string>();
        Walk(root, 0);
        return lines;

        void Walk(AutomationElement element, int depth)
        {
            lines.Add($"{new string(' ', depth * 2)}{element.Properties.ControlType.ValueOrDefault} name=\"{element.Properties.Name.ValueOrDefault}\" id=\"{element.Properties.AutomationId.ValueOrDefault}\" class=\"{element.Properties.ClassName.ValueOrDefault}\"");
            for (var child = walker.GetFirstChild(element); child is not null && depth < 40; child = walker.GetNextSibling(child))
            {
                Walk(child, depth + 1);
            }
        }
    }

    private static AutomationElement Wait(Func<AutomationElement?> find, string what) =>
        Retry.WhileNull(find, Patience).Result ?? throw new InvalidOperationException($"Couldn't find {what}.");

    /// <summary>
    /// A menu item that's showing: inside the window when UI Automation lists the open popup under its menu, or in the
    /// popup's own top-level window, which Avalonia opens for a submenu.
    /// </summary>
    private static AutomationElement? InApp(Application app, UIA3Automation automation, Window window, string name) =>
        new[] { window }
            .Concat(automation.GetDesktop().FindAllChildren(cf => cf.ByProcessId(app.ProcessId)))
            .Select(root => root.FindFirstDescendant(cf => cf.ByName(name).And(cf.ByControlType(ControlType.MenuItem))))
            .FirstOrDefault(item => item is not null && !item.IsOffscreen);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetWindowPos(nint window, nint after, int x, int y, int width, int height, uint flags);
}
