using Avalonia.Controls;
using Avalonia.Media;

namespace Hedgequill.Desktop.Views;

public partial class SaveStatus : UserControl
{
    // The prototype's states: label, icon, color, title, detail, and the two actions where there are any.
    private static readonly Dictionary<SaveState, (string Label, string Icon, string Color, string Title, string Detail, string? Primary, string? Secondary)> States = new()
    {
        [SaveState.Saving] = ("Saving…", "M8 2.5a5.5 5.5 0 1 1-5.5 5.5", "TextTertiary", "Saving", "Writing changes to disk.", null, null),
        [SaveState.Saved] = ("Saved", "M3.5 8.5L6.5 11.5L12.5 4.5", "TextTertiary", "Saved", "Every change is written to the Markdown file within a second of typing. Nothing is sent anywhere.", null, null),
        [SaveState.Failed] = ("Couldn't save", "M8 2.5a5.5 5.5 0 1 0 0 11a5.5 5.5 0 1 0 0-11ZM8 5v3.5M8 11h.01", "WarningText", "Couldn't save the last change", "Writing to the file failed. Your text is still here; keep writing and Hedgequill will retry, or save a copy somewhere else.", "Retry", "Save a copy…"),
        [SaveState.ChangedOnDisk] = ("Changed on disk", "M9.5 2H4.5a1 1 0 0 0-1 1v10a1 1 0 0 0 1 1h7a1 1 0 0 0 1-1V5ZM8 6v4M6 8h4M6 11.5h4", "InfoText", "This file changed outside Hedgequill", "Another app edited the file. You can reload it, or keep what's here and overwrite the file.", "Reload file", "Keep mine"),
        [SaveState.ReadOnly] = ("Read-only", "M4 7.5h8a.5.5 0 0 1 .5.5v5a.5.5 0 0 1-.5.5H4a.5.5 0 0 1-.5-.5V8a.5.5 0 0 1 .5-.5ZM5.5 7.5V5.5a2.5 2.5 0 0 1 5 0v2", "WarningText", "This folder is read-only", "Hedgequill can't write here. Your edits stay in memory until you choose a writable folder.", "Choose folder…", "Dismiss"),
    };

    private Saver? _saver;
    private string _path = "";

    public SaveStatus()
    {
        InitializeComponent();
        Primary.Click += (_, _) => Act(primary: true);
        Secondary.Click += (_, _) => Act(primary: false);
    }

    /// <summary>Save a copy… for this scene or page.</summary>
    public event Action<Hedgequill.Core.Projects.Node>? SaveCopyRequested;

    /// <summary>Choose folder… after a read-only save.</summary>
    public event Action? ChooseFolderRequested;

    public string Text => LabelText.Text ?? "";

    public void Attach(Saver saver, Func<string> openPath)
    {
        _saver = saver;
        saver.Changed += () => Update(openPath());
        Update(openPath());
    }

    public void ShowDetails() => Label.Flyout!.ShowAt(Label);

    public void Update(string openPath)
    {
        if (_saver is null)
        {
            return;
        }

        var state = States[_saver.State];
        _path = _saver.Problem is { } node ? _saver.PathOf(node) : openPath;
        LabelText.Text = state.Label;
        Icon.Data = Geometry.Parse(state.Icon);
        Label.Foreground = this.TryFindResource(state.Color, ActualThemeVariant, out var brush) ? brush as IBrush : null;
        Label.Classes.Set("problem", state.Primary is not null);
        DetailTitle.Text = state.Title;
        DetailText.Text = state.Detail;
        DetailPath.Text = _path;
        DetailPath.IsVisible = _path.Length > 0;
        Actions.IsVisible = state.Primary is not null;
        Primary.Content = state.Primary;
        Secondary.Content = state.Secondary;
    }

    private void Act(bool primary)
    {
        if (_saver is null)
        {
            return;
        }

        Label.Flyout!.Hide();
        switch (_saver.State, primary)
        {
            case (SaveState.Failed, true):
                _saver.Flush();
                break;
            case (SaveState.Failed, false) when _saver.Problem is { } node:
                SaveCopyRequested?.Invoke(node);
                break;
            case (SaveState.ChangedOnDisk, true):
                _saver.ReloadFile();
                break;
            case (SaveState.ChangedOnDisk, false):
                _saver.KeepMine();
                break;
            case (SaveState.ReadOnly, true):
                ChooseFolderRequested?.Invoke();
                break;
        }
    }
}
