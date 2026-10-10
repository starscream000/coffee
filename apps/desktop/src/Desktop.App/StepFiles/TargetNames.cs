// The names of the targets a file declares under its "targets" key: those of
// a test or flow (used only in that file) and those of a shared
// *.targets.yaml file. Used by the target picker of a step's form.

namespace Desktop.App.StepFiles;

/// <summary>Reads target names.</summary>
public static class TargetNames
{
    /// <summary>The ending of shared targets files.</summary>
    public const string SharedFileEnding = ".targets.yaml";

    /// <summary>The names under a file's <c>targets</c> key.</summary>
    /// <param name="text">The file's text.</param>
    /// <returns>The names, in the file's order; empty when the file has none or cannot be read.</returns>
    public static IReadOnlyList<string> Of(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        try
        {
            return YamlTree.Read(text) is YamlMapping { } root && root.Get("targets") is YamlMapping targets
                ? [.. targets.Entries.Select(e => e.Key.Value)]
                : [];
        }
        catch (YamlTreeException)
        {
            return [];
        }
    }
}
