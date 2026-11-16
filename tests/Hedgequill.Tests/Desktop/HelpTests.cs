using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Styling;
using Hedgequill.Desktop.Views;
using Hedgequill.Tests.Core;
using static Hedgequill.Tests.Desktop.Ui;

namespace Hedgequill.Tests.Desktop;

/// <summary>The Help menu: its three sheets, About, and Check for updates…, as the prototype's version 6 draws them.</summary>
public sealed class HelpTests : IDisposable
{
    private readonly TempFolder _temp = new();

    private static RawInputModifiers Command => OperatingSystem.IsMacOS() ? RawInputModifiers.Meta : RawInputModifiers.Control;

    [Fact]
    public Task Keyboard_shortcuts_lists_each_command_with_the_keys_its_menu_item_shows() => OnWindow(ThemeVariant.Light, _temp["settings"], window =>
    {
        Press(window, PhysicalKey.Slash, Command);
        var sheet = Find<HelpSheet>(window, "Help");
        Assert.True(sheet.IsVisible);
        Assert.Equal("Keyboard shortcuts", sheet.Title);

        var groups = (IReadOnlyList<ShortcutGroup>)Find<ItemsControl>(sheet, "ShortcutsPage").ItemsSource!;
        Assert.Equal(["Writing", "Project", "View", "AO3 and Help"], groups.Select(group => group.Title));
        var gestures = NativeMenu.GetMenu(window)!.Items.OfType<NativeMenuItem>()
            .SelectMany(menu => menu.Menu!.Items.OfType<NativeMenuItem>())
            .Where(item => item.Gesture is not null)
            .ToDictionary(item => item.Header!.TrimEnd('…'), item => item.Gesture!.ToString("p", null));
        Assert.All(groups.SelectMany(group => group.Rows), row => Assert.Equal(gestures[row.Label], row.Keys));
        // A Mac shows its own symbols, which only the native platform registers, so the labels themselves are checked on Windows.
        if (!OperatingSystem.IsMacOS())
        {
            Assert.Contains(new ShortcutRow("Bold", "Ctrl+B"), groups[0].Rows);
            Assert.Contains(new ShortcutRow("Keyboard shortcuts", "Ctrl+/"), groups[3].Rows);
        }

        Press(window, PhysicalKey.Escape);
        Assert.False(sheet.IsVisible);
    });

    [Theory]
    [InlineData(HelpPage.Writing, "Writing in Hedgequill", "WritingPage")]
    [InlineData(HelpPage.Ao3, "Pasting into AO3", "Ao3Page")]
    [InlineData(HelpPage.About, "About Hedgequill", "AboutPage")]
    public Task Each_help_item_opens_its_own_page(HelpPage page, string title, string shown) => OnWindow(ThemeVariant.Light, _temp["settings"], window =>
    {
        window.ShowHelp(page);
        var sheet = Find<HelpSheet>(window, "Help");
        Assert.Equal(title, sheet.Title);
        Assert.Equal([shown], new[] { "WritingPage", "ShortcutsPage", "Ao3Page", "AboutPage" }.Where(name => Find<Control>(sheet, name).IsVisible));
        if (page == HelpPage.About)
        {
            Assert.Contains(VisibleText(sheet), text => text?.StartsWith("Version ", StringComparison.Ordinal) == true);
        }

        Find<Button>(sheet, "Done").RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Assert.False(sheet.IsVisible);
    });

    [Fact]
    public Task A_copy_that_isnt_installed_says_it_cant_update_itself() => OnWindow(ThemeVariant.Light, _temp["settings"], window =>
    {
        window.CheckForUpdates().GetAwaiter().GetResult();
        Assert.Equal("This copy can't update itself", Find<Toast>(window, "Notice").Title);
        Assert.False(window.UpdateReady);
    });

    public void Dispose() => _temp.Dispose();
}
