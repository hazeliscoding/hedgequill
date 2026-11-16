using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Hedgequill.Core.Projects;
using Hedgequill.Core.Text;

namespace Hedgequill.Desktop.Views;

/// <summary>One kept version in the list: when it was kept, its words, and its text.</summary>
public sealed record VersionRow(SceneVersion Version, string When, string Words, string Text)
{
    /// <summary>Reads a version's text. A time on every label, since a day can keep several.</summary>
    public static VersionRow From(SceneVersion version, DateTime now)
    {
        var text = version.Text;
        var day = Labels.When(version.When, now);
        var when = day.StartsWith("Today", StringComparison.Ordinal) ? day : $"{day}, {version.When:HH:mm}";
        return new VersionRow(version, when, Labels.Words(Hedgequill.Core.Text.Words.Count(text)), text);
    }
}

public partial class VersionsSheet : UserControl
{
    private const string None = "No earlier versions of this scene yet. Hedgequill keeps one each time you open the project and start changing it.";

    private Action<VersionRow>? _restore;
    private Control? _returnFocus;

    public VersionsSheet()
    {
        InitializeComponent();
        Cancel.Click += (_, _) => Hide();
        Restore.Click += (_, _) =>
        {
            if (Kept.SelectedItem is VersionRow row)
            {
                Hide();
                _restore?.Invoke(row);
            }
        };
        Kept.SelectionChanged += (_, _) =>
        {
            KeptText.Text = (Kept.SelectedItem as VersionRow)?.Text ?? None;
            TextScroll.Offset = default;
        };
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

    /// <summary>Opens on a scene's versions, newest first and chosen; Bring back this version calls back with the chosen one.</summary>
    public void Show(string sceneTitle, IReadOnlyList<VersionRow> versions, Action<VersionRow> restore)
    {
        _restore = restore;
        Heading.Text = $"Earlier versions of “{sceneTitle}”";
        AutomationProperties.SetName(this, Heading.Text);
        Kept.ItemsSource = versions;
        Kept.SelectedIndex = versions.Count > 0 ? 0 : -1;
        KeptText.Text = versions.Count > 0 ? versions[0].Text : None;
        Restore.IsEnabled = versions.Count > 0;
        _returnFocus = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() as Control;
        IsVisible = true;
        Dispatcher.UIThread.Post(() => (versions.Count > 0 ? (Control)Kept : Cancel).Focus(NavigationMethod.Tab), DispatcherPriority.Loaded);
    }

    public void Hide()
    {
        IsVisible = false;
        _returnFocus?.Focus();
    }
}
