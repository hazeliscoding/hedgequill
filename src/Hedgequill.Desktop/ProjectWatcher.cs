using Avalonia.Threading;
using Hedgequill.Core.Projects;

namespace Hedgequill.Desktop;

/// <summary>
/// Watches a project's folder for Markdown files another app changes, as a sync client does after the same file was edited on a
/// phone. It hands each changed scene or page on a quarter-second after the last event, since one save can raise several.
/// </summary>
public sealed class ProjectWatcher : IDisposable
{
    private readonly Project _project;
    private readonly Action<Node> _changedOutside;
    private readonly FileSystemWatcher _watcher;
    private readonly DispatcherTimer _timer;
    private readonly HashSet<string> _changed = new(StringComparer.OrdinalIgnoreCase);

    public ProjectWatcher(Project project, Action<Node> changedOutside)
    {
        _project = project;
        _changedOutside = changedOutside;
        _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(250), DispatcherPriority.Background, (_, _) => Flush());
        _watcher = new FileSystemWatcher(project.Folder, "*.md")
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
        };
        _watcher.Changed += (_, e) => Queue(e.FullPath);
        _watcher.Created += (_, e) => Queue(e.FullPath);
        _watcher.Renamed += (_, e) => Queue(e.FullPath);
        _watcher.EnableRaisingEvents = true;
    }

    public void Dispose()
    {
        _watcher.Dispose();
        _timer.Stop();
    }

    // The watcher raises its events on a pool thread; the project belongs to the UI thread.
    private void Queue(string path) => Dispatcher.UIThread.Post(() =>
    {
        _changed.Add(path);
        _timer.Stop();
        _timer.Start();
    });

    private void Flush()
    {
        _timer.Stop();
        var paths = _changed.ToList();
        _changed.Clear();
        foreach (var path in paths)
        {
            if (_project.Root.Descendants().FirstOrDefault(node => node.IsFile && string.Equals(_project.PathOf(node), path, StringComparison.OrdinalIgnoreCase)) is { } node)
            {
                _changedOutside(node);
            }
        }
    }
}
