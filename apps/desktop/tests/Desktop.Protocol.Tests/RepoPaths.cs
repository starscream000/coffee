// Finds files of the repository from a test, wherever the test binaries run.

namespace Desktop.Protocol.Tests;

/// <summary>Paths into the repository, for tests that read its files.</summary>
internal static class RepoPaths
{
    private static readonly Lazy<string> RootPath = new(FindRoot);

    /// <summary>Absolute path of the repository root (the folder holding pnpm-workspace.yaml).</summary>
    public static string Root => RootPath.Value;

    /// <summary>Absolute path of a file or folder given relative to the repository root.</summary>
    /// <param name="relative">Path with forward slashes, such as <c>packages/protocol/schema</c>.</param>
    /// <returns>The absolute path.</returns>
    public static string Of(string relative) =>
        Path.Combine([Root, .. relative.Split('/')]);

    private static string FindRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "pnpm-workspace.yaml")))
            {
                return dir.FullName;
            }
        }

        throw new InvalidOperationException(
            $"No pnpm-workspace.yaml above {AppContext.BaseDirectory}; run the tests from inside the repository.");
    }
}
