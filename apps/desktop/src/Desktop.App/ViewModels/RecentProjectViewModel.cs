// One entry of the start page's recent projects.

namespace Desktop.App.ViewModels;

/// <summary>A recently opened project.</summary>
/// <param name="Path">The project folder.</param>
/// <param name="Exists">False when the folder is gone.</param>
public sealed record RecentProjectViewModel(string Path, bool Exists)
{
    /// <summary>The folder's name.</summary>
    public string Name => System.IO.Path.GetFileName(Path.TrimEnd('/', '\\'));
}
