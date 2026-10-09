// Access to the files of an open project: reading a step file, the fallback
// search for test files, and watching for changes. Paths given to and returned
// by this service are relative to the project root, with forward slashes, as
// the protocol writes them.

namespace Desktop.App.Services;

/// <summary>Reads and watches the files of a project.</summary>
public interface IProjectFiles
{
    /// <summary>Reads a file's text.</summary>
    /// <param name="root">The project root.</param>
    /// <param name="relativePath">The file, relative to the root.</param>
    /// <returns>The text.</returns>
    /// <exception cref="IOException">The file cannot be read.</exception>
    string ReadText(string root, string relativePath);

    /// <summary>
    /// Finds files named <c>*.test.yaml</c> under the root, skipping folders
    /// whose names start with a dot (the data folder, <c>.git</c>) and
    /// <c>node_modules</c>. Only for engines that cannot list tests yet
    /// (request R0003).
    /// </summary>
    /// <param name="root">The project root.</param>
    /// <returns>Relative paths, sorted.</returns>
    IReadOnlyList<string> FindTestFiles(string root);

    /// <summary>
    /// Watches the root for changes to YAML files and user-action sources
    /// (<see cref="ProjectFileKinds.IsWatched"/>). Changes are gathered for a
    /// short while, then reported together on a background thread.
    /// </summary>
    /// <param name="root">The project root.</param>
    /// <param name="changed">Called with the relative paths that changed, were created, deleted or renamed.</param>
    /// <returns>Stops watching when disposed.</returns>
    IDisposable Watch(string root, Action<IReadOnlyCollection<string>> changed);
}

/// <summary>The project files on disk.</summary>
public sealed class DiskProjectFiles : IProjectFiles
{
    private static readonly TimeSpan Debounce = TimeSpan.FromMilliseconds(300);

    /// <summary>Turns an absolute path under <paramref name="root"/> into the protocol's relative form.</summary>
    /// <param name="root">The project root.</param>
    /// <param name="path">An absolute path.</param>
    /// <returns>The relative path with forward slashes.</returns>
    public static string ToRelative(string root, string path) =>
        Path.GetRelativePath(root, path).Replace(Path.DirectorySeparatorChar, '/');

    /// <inheritdoc />
    public string ReadText(string root, string relativePath) =>
        File.ReadAllText(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));

    /// <inheritdoc />
    public IReadOnlyList<string> FindTestFiles(string root)
    {
        var found = new List<string>();
        var pending = new Stack<string>([root]);
        while (pending.TryPop(out var dir))
        {
            IEnumerable<string> files, dirs;
            try
            {
                files = Directory.EnumerateFiles(dir, "*.test.yaml");
                dirs = Directory.EnumerateDirectories(dir);
                found.AddRange(files.Select(f => ToRelative(root, f)));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                continue;
            }

            foreach (var sub in dirs)
            {
                var name = Path.GetFileName(sub);
                if (!name.StartsWith('.') && name != "node_modules")
                {
                    pending.Push(sub);
                }
            }
        }

        found.Sort(StringComparer.Ordinal);
        return found;
    }

    /// <inheritdoc />
    public IDisposable Watch(string root, Action<IReadOnlyCollection<string>> changed) => new Watcher(root, changed);

    private sealed class Watcher : IDisposable
    {
        private readonly FileSystemWatcher _watcher;
        private readonly Timer _timer;
        private readonly HashSet<string> _changes = new(StringComparer.Ordinal);
        private readonly Lock _gate = new();
        private readonly string _root;
        private readonly Action<IReadOnlyCollection<string>> _changed;

        public Watcher(string root, Action<IReadOnlyCollection<string>> changed)
        {
            _root = root;
            _changed = changed;
            _timer = new Timer(_ => Flush());
            _watcher = new FileSystemWatcher(root)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.DirectoryName,
            };
            _watcher.Changed += (_, e) => Add(e.FullPath);
            _watcher.Created += (_, e) => AddCreated(e.FullPath);
            _watcher.Deleted += (_, e) => Add(e.FullPath);
            _watcher.Renamed += (_, e) =>
            {
                Add(e.OldFullPath);
                Add(e.FullPath);
            };
            _watcher.EnableRaisingEvents = true;
        }

        public void Dispose()
        {
            _watcher.Dispose();
            _timer.Dispose();
        }

        /// <summary>
        /// A new folder's files can be created before the watcher watches the
        /// folder (inotify adds subfolders late), so a new folder is scanned.
        /// </summary>
        private void AddCreated(string fullPath)
        {
            if (!Directory.Exists(fullPath))
            {
                Add(fullPath);
                return;
            }

            try
            {
                foreach (var file in Directory.EnumerateFiles(fullPath, "*", SearchOption.AllDirectories))
                {
                    Add(file);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // The folder went away again; nothing to report.
            }
        }

        private void Add(string fullPath)
        {
            var relative = ToRelative(_root, fullPath);
            if (!ProjectFileKinds.IsWatched(relative))
            {
                return;
            }

            lock (_gate)
            {
                _changes.Add(relative);
            }

            _timer.Change(Debounce, Timeout.InfiniteTimeSpan);
        }

        private void Flush()
        {
            string[] batch;
            lock (_gate)
            {
                batch = [.. _changes];
                _changes.Clear();
            }

            if (batch.Length > 0)
            {
                _changed(batch);
            }
        }
    }
}
