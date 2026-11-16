using Avalonia.Threading;
using Hedgequill.Core.Files;
using Hedgequill.Core.Projects;

namespace Hedgequill.Desktop;

/// <summary>The prototype's save states. Couldn't save, Changed on disk and Read-only keep the text in memory until the writer chooses.</summary>
public enum SaveState
{
    Saved,
    Saving,
    Failed,
    ChangedOnDisk,
    ReadOnly,
}

/// <summary>Autosave: a change is written within a second of the last keystroke, and at once before anything moves or closes the file.</summary>
public sealed class Saver
{
    private readonly Project _project;
    private readonly Action _commit;
    // About ten seconds of retries at the autosave delay.
    private const int HeldTries = 15;

    private readonly HashSet<Node> _unsaved = [];
    private readonly DispatcherTimer _timer;
    private int _heldTries;

    /// <param name="commit">Copies the editor's text into the open node before a write.</param>
    public Saver(Project project, Action commit, TimeSpan delay)
    {
        _project = project;
        _commit = commit;
        _timer = new DispatcherTimer { Interval = delay };
        _timer.Tick += (_, _) => Flush();
    }

    public event Action? Changed;

    /// <summary>Reload file replaced a node's text, so the editor must show it.</summary>
    public event Action<Node>? Reloaded;

    public SaveState State { get; private set; } = SaveState.Saved;

    /// <summary>The scene or page the current problem is about.</summary>
    public Node? Problem { get; private set; }

    public string Detail { get; private set; } = "";

    public bool HasUnsaved => _unsaved.Count > 0;

    public string PathOf(Node node) => _project.PathOf(node);

    public void Edited(Node node)
    {
        _unsaved.Add(node);
        if (State == SaveState.Saved)
        {
            Set(SaveState.Saving, null, "");
        }

        _timer.Stop();
        _timer.Start();
    }

    /// <summary>Writes every unsaved scene and page now. A file another app changed waits for Reload file or Keep mine.</summary>
    public void Flush()
    {
        _timer.Stop();
        _commit();
        foreach (var node in _unsaved.ToList())
        {
            if (node.Parent is null)
            {
                // Deleted after its last flush; Trash/ holds the file it had.
                _unsaved.Remove(node);
                continue;
            }

            if (State == SaveState.ChangedOnDisk && node == Problem)
            {
                continue;
            }

            if (!Write(node, overwrite: false))
            {
                return;
            }
        }

        if (State != SaveState.ChangedOnDisk || !_unsaved.Contains(Problem!))
        {
            Set(_unsaved.Count == 0 ? SaveState.Saved : SaveState.Saving, null, "");
        }
    }

    /// <summary>
    /// Another app changed a scene's or page's file. With no unsaved edits it reloads, and the editor shows the new text; with
    /// some, it's Changed on disk at once, without waiting for the next save.
    /// </summary>
    public void ChangedOutside(Node node)
    {
        string disk;
        try
        {
            disk = File.ReadAllText(_project.PathOf(node)).ReplaceLineEndings("\n");
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // Still being written, or gone; the next event or save sees it.
            return;
        }

        // Hedgequill's own save, or nothing new.
        if (disk == node.SavedText || node.Parent is null)
        {
            return;
        }

        _commit();
        if (!_unsaved.Contains(node) && node.Text == node.SavedText)
        {
            _project.Reload(node);
            Reloaded?.Invoke(node);
        }
        else
        {
            Set(SaveState.ChangedOnDisk, node, "");
        }
    }

    public void KeepMine()
    {
        if (Problem is { } node)
        {
            _commit();
            if (Write(node, overwrite: true))
            {
                Set(SaveState.Saving, null, "");
                Flush();
            }
        }
    }

    public void ReloadFile()
    {
        if (Problem is not { } node)
        {
            return;
        }

        try
        {
            _project.Reload(node);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            Set(SaveState.Failed, node, error.Message);
            return;
        }

        _unsaved.Remove(node);
        Reloaded?.Invoke(node);
        Set(SaveState.Saving, null, "");
        Flush();
    }

    private bool Write(Node node, bool overwrite)
    {
        try
        {
            _project.Save(node, overwrite);
            _unsaved.Remove(node);
            _heldTries = 0;
            return true;
        }
        catch (ChangedOnDiskException)
        {
            Set(SaveState.ChangedOnDisk, node, "");
        }
        catch (FileHeldException error)
        {
            // A sync client holds a file for a moment while it uploads; keep trying quietly before calling it a failure.
            if (++_heldTries < HeldTries)
            {
                _timer.Stop();
                _timer.Start();
                return false;
            }

            Set(SaveState.Failed, node, error.Message);
        }
        catch (UnauthorizedAccessException error)
        {
            Set(SaveState.ReadOnly, node, error.Message);
        }
        catch (IOException error)
        {
            Set(SaveState.Failed, node, error.Message);
        }

        return false;
    }

    private void Set(SaveState state, Node? problem, string detail)
    {
        if (State == state && Problem == problem && Detail == detail)
        {
            return;
        }

        State = state;
        Problem = problem;
        Detail = detail;
        Changed?.Invoke();
    }
}
