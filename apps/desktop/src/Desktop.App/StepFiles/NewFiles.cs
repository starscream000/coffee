// Names and first texts of the files the app creates: a test, a flow or a
// shared targets file. A new test or flow has one step, because the engine
// needs at least one ("steps" needs at least one step); a targets file starts
// empty. The name and folder are checked here, before anything is written.

using System.Globalization;

namespace Desktop.App.StepFiles;

/// <summary>The kinds of file the app creates.</summary>
public enum NewFileKind
{
    /// <summary>A test (<c>*.test.yaml</c>).</summary>
    Test,

    /// <summary>A flow (<c>*.flow.yaml</c>).</summary>
    Flow,

    /// <summary>A shared targets file (<c>*.targets.yaml</c>).</summary>
    Targets,
}

/// <summary>Names and templates of new files.</summary>
public static class NewFiles
{
    /// <summary>The ending of a kind's file names.</summary>
    /// <param name="kind">The kind.</param>
    /// <returns>Such as <c>.test.yaml</c>.</returns>
    public static string Ending(NewFileKind kind) => kind switch
    {
        NewFileKind.Test => ".test.yaml",
        NewFileKind.Flow => ".flow.yaml",
        _ => TargetNames.SharedFileEnding,
    };

    /// <summary>The folder a kind's new files go to when nothing else is chosen.</summary>
    /// <param name="kind">The kind.</param>
    /// <returns><c>tests</c>, <c>flows</c> or <c>targets</c>, as in the demo project.</returns>
    public static string DefaultFolder(NewFileKind kind) => kind switch
    {
        NewFileKind.Test => "tests",
        NewFileKind.Flow => "flows",
        _ => "targets",
    };

    /// <summary>The kind of an existing file, from its name.</summary>
    /// <param name="path">The file's path.</param>
    /// <returns>The kind, or null for another file.</returns>
    public static NewFileKind? KindOf(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return Enum.GetValues<NewFileKind>().Cast<NewFileKind?>().FirstOrDefault(k => path.EndsWith(Ending(k!.Value), StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>The project-relative path of a new file from the folder and name typed.</summary>
    /// <param name="folder">The folder, relative to the project root; empty for the root.</param>
    /// <param name="name">The name, with or without its ending, such as <c>guest-checkout</c>.</param>
    /// <param name="kind">The kind of file.</param>
    /// <returns>Such as <c>tests/checkout/guest-checkout.test.yaml</c>.</returns>
    /// <exception cref="ArgumentException">The name is empty, or the name or folder is not a plain path inside the project; the message says why.</exception>
    public static string PathFor(string folder, string name, NewFileKind kind)
    {
        ArgumentNullException.ThrowIfNull(folder);
        ArgumentNullException.ThrowIfNull(name);
        var file = name.Trim();
        if (file.Length == 0)
        {
            throw new ArgumentException("Enter a name for the file.", nameof(name));
        }

        if (file.Contains('/') || file.Contains('\\'))
        {
            throw new ArgumentException("Enter the name without folders; put the folder in its own field.", nameof(name));
        }

        if (KindOf(file) is { } other && other != kind)
        {
            throw new ArgumentException($"The name of a {Words(kind)} ends with {Ending(kind)}, not {Ending(other)}.", nameof(name));
        }

        if (!file.EndsWith(Ending(kind), StringComparison.OrdinalIgnoreCase))
        {
            file += Ending(kind);
        }

        var parts = folder.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Append(file).ToList();
        foreach (var part in parts)
        {
            if (part is "." or ".." || part.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || part.IndexOfAny(['<', '>', ':', '"', '|', '?', '*']) >= 0)
            {
                throw new ArgumentException($"\"{part}\" cannot be part of a file's path in the project.", nameof(folder));
            }
        }

        if (folder.TrimStart().StartsWith('/') || folder.TrimStart().StartsWith('\\') || (folder.Length > 1 && folder[1] == ':'))
        {
            throw new ArgumentException("Enter the folder relative to the project, such as tests/checkout.", nameof(folder));
        }

        return string.Join('/', parts);
    }

    /// <summary>What a kind is called, for people.</summary>
    /// <param name="kind">The kind.</param>
    /// <returns>"test", "flow" or "targets file".</returns>
    public static string Words(NewFileKind kind) => kind == NewFileKind.Targets ? "targets file" : kind.ToString().ToLowerInvariant();

    /// <summary>The first text of a new file.</summary>
    /// <param name="kind">The kind.</param>
    /// <param name="path">The file's path, for the test's or flow's name.</param>
    /// <returns>A file the engine reads without problems.</returns>
    /// <example><c>NewFiles.Template(NewFileKind.Test, "tests/guest-checkout.test.yaml")</c> names the test "Guest checkout".</example>
    public static string Template(NewFileKind kind, string path) => kind switch
    {
        NewFileKind.Targets => "version: 1\ntargets: {}\n",
        _ => $"version: 1\nname: {StepWriter.Text(DisplayName(path, kind))}\nsteps:\n  - goto: /\n",
    };

    /// <summary>A readable name from a file name: <c>guest-checkout.test.yaml</c> gives "Guest checkout".</summary>
    /// <param name="path">The file's path.</param>
    /// <param name="kind">The kind, for the ending to drop.</param>
    /// <returns>The name.</returns>
    public static string DisplayName(string path, NewFileKind kind)
    {
        ArgumentNullException.ThrowIfNull(path);
        var file = path[(path.LastIndexOf('/') + 1)..];
        var bare = file.EndsWith(Ending(kind), StringComparison.OrdinalIgnoreCase) ? file[..^Ending(kind).Length] : file;
        var words = bare.Replace('-', ' ').Replace('_', ' ').Trim();
        return words.Length == 0 ? "New " + kind.ToString().ToLowerInvariant() : char.ToUpper(words[0], CultureInfo.InvariantCulture) + words[1..];
    }
}
