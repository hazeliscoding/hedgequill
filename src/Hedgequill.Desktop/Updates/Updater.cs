using System.Reflection;
using Velopack;
using Velopack.Sources;

namespace Hedgequill.Desktop.Updates;

/// <summary>What a check for updates found.</summary>
public enum UpdateCheck
{
    UpToDate,
    Ready,
    NotInstalled,
    Failed,
}

/// <summary>
/// The update check, the app's one network request: it asks GitHub Releases for a newer Hedgequill and sends nothing about
/// the writer or their words. It downloads in the background, and the update installs when Hedgequill closes, or when the
/// writer chooses to restart. It never restarts on its own.
/// </summary>
public sealed class Updater
{
    private const string Repository = "https://github.com/hazeliscoding/hedgequill";

    private readonly IUpdateSource _source;
    private UpdateManager? _manager;
    private Task<UpdateCheck>? _checking;
    private UpdateInfo? _ready;

    /// <param name="feed">
    /// A local folder of Velopack releases to read instead of GitHub, which the release workflow's update test sets through
    /// HEDGEQUILL_UPDATE_FEED. A network share is ignored, so it can't become a second way onto the network.
    /// </param>
    public Updater(string? feed = null)
    {
        _source = feed is { Length: > 0 } && Path.IsPathFullyQualified(feed) && !new Uri(feed, UriKind.Absolute).IsUnc
            ? new SimpleFileSource(new DirectoryInfo(feed))
            : new GithubSource(Repository, accessToken: null, prerelease: false);
    }

    /// <summary>This copy's version, as the release workflow stamped it.</summary>
    public static string Version { get; } =
        Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0] ?? "0.0.0";

    /// <summary>The downloaded version waiting to install, or null.</summary>
    public string? ReadyVersion => _ready?.TargetFullRelease.Version.ToString();

    /// <summary>
    /// Checks GitHub Releases and downloads what it finds; <paramref name="downloading"/> hears the version first. A check
    /// already running is joined rather than repeated.
    /// </summary>
    public Task<UpdateCheck> CheckAsync(Action<string>? downloading = null) =>
        _checking is { IsCompleted: false } running ? running : _checking = Run(downloading);

    /// <summary>
    /// Hands a downloaded update to Velopack's updater, which waits for Hedgequill to exit, installs it, and starts it again
    /// with <paramref name="restartArgs"/> only when the writer chose Restart.
    /// </summary>
    public void InstallOnExit(bool restart, params string[] restartArgs)
    {
        if (_ready is { } update)
        {
            _manager!.WaitExitThenApplyUpdates(update.TargetFullRelease, silent: true, restart, restartArgs);
        }
    }

    private UpdateManager? Manager()
    {
        try
        {
            return _manager ??= new UpdateManager(_source);
        }
        catch (InvalidOperationException)
        {
            // Only Program runs VelopackApp; the tests and the probes open the window without it.
            return null;
        }
    }

    private async Task<UpdateCheck> Run(Action<string>? downloading)
    {
        try
        {
            if (_ready is not null)
            {
                return UpdateCheck.Ready;
            }

            // A copy run from a build folder has nothing to update.
            if (Manager() is not { IsInstalled: true } manager)
            {
                return UpdateCheck.NotInstalled;
            }

            if (await manager.CheckForUpdatesAsync() is not { } found)
            {
                return UpdateCheck.UpToDate;
            }

            downloading?.Invoke(found.TargetFullRelease.Version.ToString());
            await manager.DownloadUpdatesAsync(found);
            _ready = found;
            return UpdateCheck.Ready;
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            // Offline, GitHub's rate limit, or a full disk: the next check tries again.
            return UpdateCheck.Failed;
        }
    }
}
