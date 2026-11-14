using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Hedgequill.Core.Projects;
using Hedgequill.Desktop.Views;
using Hedgequill.Tests.Core;
using static Hedgequill.Tests.Desktop.Ui;

namespace Hedgequill.Tests.Desktop;

public sealed class WelcomeTests : IDisposable
{
    private readonly TempFolder _temp = new();

    [Fact]
    public Task The_welcome_screen_shows_the_prototypes_copy() => OnWindow(ThemeVariant.Light, _temp["settings"], window =>
    {
        Assert.Equal(
            ["Hedgequill", "A quiet place to write.", "New project…", "Open folder…",
             "A project is a folder of Markdown files on your computer. Nothing leaves it unless you copy it out.",
             "Recent", "Nothing here yet.", "Projects you open will be listed here."],
            VisibleText(Find<ContentControl>(window, "Screen")));
        Assert.Equal("Hedgequill", window.Title);
        Assert.Empty(Errors.Messages);
    });

    [Fact]
    public Task The_binding_error_check_catches_a_broken_binding() => OnWindow(
        ThemeVariant.Light,
        _ => Assert.NotEmpty(Errors.Messages),
        () => new Window { DataContext = new object(), Content = new TextBlock { [!TextBlock.TextProperty] = new ReflectionBinding("Missing") } });

    [Theory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public Task The_window_paints_its_theme_tokens(string variant)
    {
        var theme = variant == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light;
        return OnWindow(theme, _temp["settings"], window =>
        {
            var frame = Render(window);
            Assert.Equal(Token(window, "BgWindow", theme), Pixel(frame, new Point(8, 8)));

            var card = window.GetVisualDescendants().OfType<Border>().Single(b => b.MaxWidth == 440);
            var inside = card.TranslatePoint(new Point(card.Bounds.Width / 2, card.Bounds.Height - 6), window)!.Value;
            Assert.Equal(Token(window, "BgPage", theme), Pixel(frame, inside));
        });
    }

    // Avalonia falls back to the system font without a word when an embedded one fails to load.
    [Fact]
    public Task The_title_and_tagline_use_the_bundled_fonts() => OnWindow(ThemeVariant.Light, _temp["settings"], window =>
    {
        var blocks = Find<ContentControl>(window, "Screen").GetVisualDescendants().OfType<TextBlock>().Where(t => t.IsEffectivelyVisible).ToDictionary(t => t.Text ?? "");
        Assert.Equal(("Gloock", FontStyle.Normal), Face(blocks["Hedgequill"]));
        Assert.Equal(("Cardo", FontStyle.Italic), Face(blocks["A quiet place to write."]));
        Assert.Equal(("Cardo", FontStyle.Italic), Face(blocks["Nothing here yet."]));
    });

    [Fact]
    public Task Tab_reaches_New_project_then_Open_folder() => OnWindow(ThemeVariant.Light, _temp["settings"], window =>
    {
        var order = new List<string?>();
        for (var i = 0; i < 2; i++)
        {
            Press(window, PhysicalKey.Tab);
            order.Add(window.FocusManager?.GetFocusedElement() is Control focused ? AutomationProperties.GetName(focused) : null);
        }

        Assert.Equal(["New project…", "Open folder…"], order);
    });

    [Fact]
    public Task Recent_lists_projects_that_still_exist_newest_first()
    {
        var recent = new RecentProjects(_temp["settings"]);
        var older = Project.Create(_temp["stories"], "Small Hours");
        older.Scenes.Single().Text = "Three invented words";
        recent.Add(older, DateTimeOffset.Now.AddDays(-1));
        var opened = DateTimeOffset.Now;
        recent.Add(Project.Create(_temp["stories"], "After the Last Train"), opened);
        var gone = Project.Create(_temp["stories"], "Gone Astray");
        recent.Add(gone, DateTimeOffset.Now);
        Directory.Delete(gone.Folder, recursive: true);

        return OnWindow(ThemeVariant.Light, _temp["settings"], window =>
        {
            var text = VisibleText(window);
            var start = text.IndexOf("Recent") + 1;
            Assert.Equal(
                ["A", "After the Last Train", _temp["stories/After the Last Train"], $"Today, {opened.ToString("HH:mm", CultureInfo.InvariantCulture)}", "0 words",
                 "S", "Small Hours", _temp["stories/Small Hours"], "Yesterday", "3 words"],
                text[start..]);
            Assert.Equal(("Cardo", FontStyle.Normal), Face(window.GetVisualDescendants().OfType<TextBlock>().First(t => t.Text == "Small Hours")));
        });
    }

    [Fact]
    public Task Choosing_a_recent_project_opens_it()
    {
        var project = Project.Create(_temp["stories"], "Small Hours");
        new RecentProjects(_temp["settings"]).Add(project, DateTimeOffset.Now.AddDays(-3));

        return OnWindow(ThemeVariant.Light, _temp["settings"], window =>
        {
            Click(window, Visible<Button>(window).Single(b => AutomationProperties.GetName(b) == "Small Hours"));

            Assert.Equal(project.Folder, window.Project?.Folder);
            Assert.Equal("Small Hours — Hedgequill", window.Title);
            Assert.True(new RecentProjects(_temp["settings"]).Load().Single().Opened > DateTimeOffset.Now.AddMinutes(-1));
        });
    }

    [Fact]
    public Task A_project_that_cant_open_leaves_the_welcome_screen_with_a_notice()
    {
        _temp.Write("stories/From the Future/hedgequill.json", """{ "format": 99 }""");

        return OnWindow(ThemeVariant.Light, _temp["settings"], window =>
        {
            Assert.False(window.Open(_temp["stories/From the Future"]));

            Assert.Null(window.Project);
            Assert.IsType<WelcomeView>(Find<ContentControl>(window, "Screen").Content);
            Assert.Contains("Couldn't open the project", VisibleText(window));
            Assert.Contains("A newer version of Hedgequill made this project. Update Hedgequill to open it.", VisibleText(window));
        });
    }

    public void Dispose() => _temp.Dispose();

    private static (string Family, FontStyle Style) Face(TextBlock block)
    {
        var glyphs = new Typeface(block.FontFamily, block.FontStyle, block.FontWeight).GlyphTypeface;
        return (glyphs.FamilyName, glyphs.Style);
    }
}
