// Access to the files of an open project: reading and writing a step file,
// finding files by their ending (such as the shared targets files), and
// watching for changes. Paths given to and returned
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

    /// <summary>Reads a file's text, or returns null when the file does not exist.</summary>
    /// <param name="root">The project root.</param>
    /// <param name="relativePath">The file, relative to the root.</param>
    /// <returns>The text, or null.</returns>
    /// <exception cref="IOException">The file exists but cannot be read.</exception>
    string? TryReadText(string root, string relativePath);

    /// <summary>
    /// Writes a file as UTF-8 without a byte-order mark, in one step: the text
    /// goes to a temporary file next to it, which then replaces the file, so a
    /// crash never leaves half a file. Creates the file (and its folder) when it
    /// does not exist.
    /// </summary>
    /// <param name="root">The project root.</param>
    /// <param name="relativePath">The file, relative to the root.</param>
    /// <param name="text">The whole text, with the line endings it should have.</param>
    /// <exception cref="IOException">The file could not be written; it is unchanged.</exception>
    /// <exception cref="UnauthorizedAccessException">The file or its folder may not be written.</exception>
    void WriteText(string root, string relativePath, string text);

    /// <summary>
    /// Watches the root for changes to YAML files and user-action sources
    /// (<see cref="ProjectFileKinds.IsWatched"/>). Changes are gathered for a
    /// short while, then reported together on a background thread.
    /// </summary>
    /// <param name="root">The project root.</param>
    /// <param name="changed">Called with the relative paths that changed, were created, deleted or renamed.</param>
    /// <returns>Stops watching when disposed.</returns>
    IDisposable Watch(string root, Action<IReadOnlyCollection<string>> changed);

    /// <summary>
    /// Finds the files under the root whose names end with a text, leaving out
    /// the data folder, <c>node_modules</c> and folders whose names start with a dot.
    /// </summary>
    /// <param name="root">The project root.</param>
    /// <param name="ending">The end of the file name, such as <c>.targets.yaml</c>; compared without regard to case.</param>
    /// <returns>Relative paths, sorted; empty when the root cannot be read.</returns>
    IReadOnlyList<string> FindFiles(string root, string ending);
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
    public string? TryReadText(string root, string relativePath)
    {
        try
        {
            return ReadText(root, relativePath);
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            return null;
        }
    }

    /// <inheritdoc />
    public void WriteText(string root, string relativePath, string text)
    {
        var path = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        var folder = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(folder);
        var temporary = Path.Combine(folder, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllText(temporary, text, new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }

    /// <inheritdoc />
    public IDisposable Watch(string root, Action<IReadOnlyCollection<string>> changed) => new Watcher(root, changed);

    /// <inheritdoc />
    public IReadOnlyList<string> FindFiles(string root, string ending)
    {
        var found = new List<string>();
        var folders = new Stack<string>([root]);
        while (folders.TryPop(out var folder))
        {
            try
            {
                found.AddRange(Directory.EnumerateFiles(folder).Where(f => Path.GetFileName(f).EndsWith(ending, StringComparison.OrdinalIgnoreCase)).Select(f => ToRelative(root, f)));
                foreach (var sub in Directory.EnumerateDirectories(folder))
                {
                    var name = Path.GetFileName(sub);
                    if (!name.StartsWith('.') && name != "node_modules")
                    {
                        folders.Push(sub);
                    }
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // A folder that vanished or may not be read has nothing to offer.
            }
        }

        found.Sort(StringComparer.Ordinal);
        return found;
    }

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
