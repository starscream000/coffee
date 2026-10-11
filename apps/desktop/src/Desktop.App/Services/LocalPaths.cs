// Paths as this computer writes them. The engine sends paths with forward
// slashes (C:/Users/…, ADR 0015 of the engine); the app turns every path it
// receives into the native form once, where it arrives, and compares paths as
// paths: case-insensitively on Windows and macOS, ignoring a trailing slash.

namespace Desktop.App.Services;

/// <summary>Turns paths from the engine into native ones and compares paths.</summary>
public static class LocalPaths
{
    private static StringComparison Comparison =>
        OperatingSystem.IsLinux() ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

    /// <summary>A path the engine sent, as this computer writes it.</summary>
    /// <param name="path">An absolute path, possibly with forward slashes.</param>
    /// <returns>The full path with native separators.</returns>
    /// <example><c>LocalPaths.FromEngine("C:/work/shop/.cfe/runs/1")</c> gives <c>C:\work\shop\.cfe\runs\1</c> on Windows.</example>
    public static string FromEngine(string path) => Path.GetFullPath(path);

    /// <summary>Whether two paths name the same file or folder.</summary>
    /// <param name="a">A path.</param>
    /// <param name="b">Another path.</param>
    /// <returns><see langword="true"/> when they are the same once made full and native.</returns>
    public static bool Same(string a, string b) =>
        string.Equals(Normal(a), Normal(b), Comparison);

    /// <summary>Whether a path is a folder or lies inside it.</summary>
    /// <param name="path">A path.</param>
    /// <param name="folder">A folder.</param>
    /// <returns><see langword="true"/> when <paramref name="path"/> is <paramref name="folder"/> or below it.</returns>
    public static bool IsInside(string path, string folder)
    {
        var inner = Normal(path);
        var outer = Normal(folder);
        return string.Equals(inner, outer, Comparison)
            || inner.StartsWith(outer + Path.DirectorySeparatorChar, Comparison);
    }

    private static string Normal(string path) =>
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
}
