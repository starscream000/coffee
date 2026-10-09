// One row of the test explorer: a folder or a test file.

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Desktop.Protocol.Messages;

namespace Desktop.App.ViewModels;

/// <summary>A folder or a test file in the test explorer.</summary>
public sealed partial class ExplorerNodeViewModel : ObservableObject
{
    private ExplorerNodeViewModel(string name, string path, TestInfo? test)
    {
        Name = name;
        Path = path;
        Test = test;
    }

    /// <summary>The folder or file name.</summary>
    public string Name { get; }

    /// <summary>Path relative to the project root, with forward slashes.</summary>
    public string Path { get; }

    /// <summary>What the engine said about the test, when it could list tests.</summary>
    public TestInfo? Test { get; }

    /// <summary>True for a folder.</summary>
    public bool IsFolder => !IsTest;

    /// <summary>True for a test file.</summary>
    public bool IsTest { get; private init; }

    /// <summary>The test's name, else the file name.</summary>
    public string DisplayName => Test?.Name ?? Name;

    /// <summary>The test's tags.</summary>
    public IReadOnlyList<string> Tags => Test?.Tags ?? [];

    /// <summary>True when the test has tags.</summary>
    public bool HasTags => Tags.Count > 0;

    /// <summary>"3 rows" for a test with several data rows, else empty.</summary>
    public string RowsText => Test is { Rows: > 1 } t ? $"{t.Rows} rows" : string.Empty;

    /// <summary>Folders and files inside a folder.</summary>
    public ObservableCollection<ExplorerNodeViewModel> Children { get; } = [];

    /// <summary>Whether a folder is open.</summary>
    [ObservableProperty]
    private bool _isExpanded = true;

    /// <summary>Errors in this file, or in the files below this folder.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasErrors))]
    private int _errorCount;

    /// <summary>Warnings in this file, or in the files below this folder.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasWarningsOnly))]
    private int _warningCount;

    /// <summary>True when there is at least one error.</summary>
    public bool HasErrors => ErrorCount > 0;

    /// <summary>True when there are warnings and no errors.</summary>
    public bool HasWarningsOnly => ErrorCount == 0 && WarningCount > 0;

    /// <summary>Creates a folder node.</summary>
    /// <param name="name">The folder's name.</param>
    /// <param name="path">Its relative path.</param>
    /// <returns>The node.</returns>
    public static ExplorerNodeViewModel Folder(string name, string path) => new(name, path, null);

    /// <summary>Creates a test file node.</summary>
    /// <param name="path">The file's relative path.</param>
    /// <param name="test">What the engine said about it, if anything.</param>
    /// <returns>The node.</returns>
    public static ExplorerNodeViewModel File(string path, TestInfo? test) =>
        new(path[(path.LastIndexOf('/') + 1)..], path, test) { IsTest = true };

    /// <summary>Sets the problem counts of this node and its children from per-file counts, and returns this node's totals.</summary>
    /// <param name="counts">Errors and warnings per relative file path.</param>
    /// <returns>This node's errors and warnings.</returns>
    public (int Errors, int Warnings) ApplyCounts(IReadOnlyDictionary<string, (int Errors, int Warnings)> counts)
    {
        ArgumentNullException.ThrowIfNull(counts);
        var total = IsTest && counts.TryGetValue(Path, out var own) ? own : (0, 0);
        foreach (var child in Children)
        {
            var (errors, warnings) = child.ApplyCounts(counts);
            total = (total.Item1 + errors, total.Item2 + warnings);
        }

        ErrorCount = total.Item1;
        WarningCount = total.Item2;
        return total;
    }
}
