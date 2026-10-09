// Which files of a project matter to the app when they change: step files
// (YAML), the config, and the source files of user actions.

using Desktop.Protocol;

namespace Desktop.App.Services;

/// <summary>Classifies project-relative paths (forward slashes) by what a change to them means.</summary>
public static class ProjectFileKinds
{
    private static readonly string[] ActionSourceExtensions = [".ts", ".mts", ".cts", ".js", ".mjs", ".cjs"];

    /// <summary>
    /// True for paths inside a folder whose name starts with a dot (the data
    /// folder, <c>.git</c>) or inside <c>node_modules</c>: the app ignores them.
    /// </summary>
    /// <param name="relativePath">A path relative to the project root.</param>
    /// <returns>Whether the path is ignored.</returns>
    public static bool IsIgnored(string relativePath)
    {
        ArgumentNullException.ThrowIfNull(relativePath);
        var folders = relativePath.Split('/')[..^1];
        return folders.Any(part => part.StartsWith('.') || part == "node_modules");
    }

    /// <summary>True for a YAML file (tests, flows, targets, the config).</summary>
    /// <param name="relativePath">A path relative to the project root.</param>
    /// <returns>Whether it is a YAML file.</returns>
    public static bool IsYaml(string relativePath) =>
        relativePath.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase) || relativePath.EndsWith(".yml", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// True for a file that may hold a user action (TypeScript or JavaScript).
    /// The engine loads actions only in <c>openProject</c>, so a change to one
    /// means opening the project again.
    /// </summary>
    /// <param name="relativePath">A path relative to the project root.</param>
    /// <returns>Whether it is an action source file.</returns>
    public static bool IsActionSource(string relativePath) =>
        ActionSourceExtensions.Any(e => relativePath.EndsWith(e, StringComparison.OrdinalIgnoreCase))
        && !relativePath.EndsWith(".d.ts", StringComparison.OrdinalIgnoreCase);

    /// <summary>True when a change to this file means opening the project again: the config or an action source.</summary>
    /// <param name="relativePath">A path relative to the project root.</param>
    /// <returns>Whether the project must be reopened.</returns>
    public static bool NeedsReopen(string relativePath) =>
        relativePath == Product.ConfigFile || IsActionSource(relativePath);

    /// <summary>True for a file the watcher reports: YAML or action sources, outside ignored folders.</summary>
    /// <param name="relativePath">A path relative to the project root.</param>
    /// <returns>Whether a change to it is reported.</returns>
    public static bool IsWatched(string relativePath) =>
        !IsIgnored(relativePath) && (IsYaml(relativePath) || IsActionSource(relativePath));
}
