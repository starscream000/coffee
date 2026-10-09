// The test explorer: the project's test files as a folder tree, filtered by a
// search text and a tag. Its source is the engine's listTests, or, for engines
// that cannot list tests yet, the files named *.test.yaml (request R0003).

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Desktop.Protocol.Messages;

namespace Desktop.App.ViewModels;

/// <summary>The test explorer.</summary>
public sealed partial class TestExplorerViewModel : ObservableObject
{
    /// <summary>The tag choice that shows every test.</summary>
    public const string AllTags = "All tags";

    private IReadOnlyList<(string File, TestInfo? Test)> _tests = [];
    private IReadOnlyDictionary<string, (int Errors, int Warnings)> _counts = new Dictionary<string, (int, int)>();

    /// <summary>The top level of the tree, after filtering.</summary>
    public ObservableCollection<ExplorerNodeViewModel> Roots { get; } = [];

    /// <summary>The tags of all tests, sorted, after <see cref="AllTags"/>.</summary>
    public ObservableCollection<string> Tags { get; } = [AllTags];

    /// <summary>Raised when the user picks a test file to look at.</summary>
    public event EventHandler<string>? FileActivated;

    /// <summary>Every test file, relative to the project root, sorted.</summary>
    public IReadOnlyList<string> Files => [.. _tests.Select(t => t.File)];

    /// <summary>Shows tests whose name or path contains this text (ignoring case).</summary>
    [ObservableProperty]
    private string _searchText = string.Empty;

    /// <summary>Shows tests with this tag; <see cref="AllTags"/> for all.</summary>
    [ObservableProperty]
    private string _selectedTag = AllTags;

    /// <summary>The selected node.</summary>
    [ObservableProperty]
    private ExplorerNodeViewModel? _selectedNode;

    /// <summary>A note above the tree, such as why names and tags are missing.</summary>
    [ObservableProperty]
    private string? _notice;

    /// <summary>"12 tests" or "4 of 12 tests".</summary>
    [ObservableProperty]
    private string _summary = string.Empty;

    /// <summary>Shows what the engine listed.</summary>
    /// <param name="tests">The engine's tests.</param>
    public void Load(IReadOnlyList<TestInfo> tests)
    {
        ArgumentNullException.ThrowIfNull(tests);
        Notice = null;
        Set([.. tests.OrderBy(t => t.File, StringComparer.Ordinal).Select(t => (t.File, (TestInfo?)t))]);
    }

    /// <summary>Shows test files found on disk, for an engine that cannot list tests.</summary>
    /// <param name="files">Relative paths.</param>
    public void LoadFallback(IReadOnlyList<string> files)
    {
        ArgumentNullException.ThrowIfNull(files);
        Notice = "This engine cannot list tests yet, so files named *.test.yaml are shown by path, without names or tags. Files outside the config's \"tests\" patterns may be listed too.";
        Set([.. files.Order(StringComparer.Ordinal).Select(f => (f, (TestInfo?)null))]);
    }

    /// <summary>Shows problem counts per file on the tree.</summary>
    /// <param name="counts">Errors and warnings per relative path.</param>
    public void ApplyProblemCounts(IReadOnlyDictionary<string, (int Errors, int Warnings)> counts)
    {
        _counts = counts;
        foreach (var root in Roots)
        {
            root.ApplyCounts(counts);
        }
    }

    partial void OnSearchTextChanged(string value) => Rebuild();

    partial void OnSelectedTagChanged(string value) => Rebuild();

    partial void OnSelectedNodeChanged(ExplorerNodeViewModel? value)
    {
        if (value is { IsTest: true })
        {
            FileActivated?.Invoke(this, value.Path);
        }
    }

    private void Set(IReadOnlyList<(string File, TestInfo? Test)> tests)
    {
        _tests = tests;
        var tags = tests.SelectMany(t => t.Test?.Tags ?? []).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        var keep = tags.Contains(SelectedTag, StringComparer.Ordinal) ? SelectedTag : AllTags;
        Tags.Clear();
        Tags.Add(AllTags);
        foreach (var tag in tags)
        {
            Tags.Add(tag);
        }

        SelectedTag = keep;
        Rebuild();
    }

    private void Rebuild()
    {
        var shown = _tests.Where(Matches).ToList();
        Roots.Clear();
        var folders = new Dictionary<string, ExplorerNodeViewModel>(StringComparer.Ordinal);
        foreach (var (file, test) in shown)
        {
            var parts = file.Split('/');
            var siblings = Roots;
            for (var i = 0; i < parts.Length - 1; i++)
            {
                var path = string.Join('/', parts[..(i + 1)]);
                if (!folders.TryGetValue(path, out var folder))
                {
                    folder = ExplorerNodeViewModel.Folder(parts[i], path);
                    folders[path] = folder;
                    InsertSorted(siblings, folder);
                }

                siblings = folder.Children;
            }

            InsertSorted(siblings, ExplorerNodeViewModel.File(file, test));
        }

        Summary = shown.Count == _tests.Count ? Plural(_tests.Count) : $"{shown.Count} of {Plural(_tests.Count)}";
        ApplyProblemCounts(_counts);
    }

    private bool Matches((string File, TestInfo? Test) entry)
    {
        if (SelectedTag != AllTags && !(entry.Test?.Tags.Contains(SelectedTag, StringComparer.Ordinal) ?? false))
        {
            return false;
        }

        var search = SearchText.Trim();
        return search.Length == 0
            || entry.File.Contains(search, StringComparison.OrdinalIgnoreCase)
            || (entry.Test?.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false);
    }

    /// <summary>Folders before files, each in name order.</summary>
    private static void InsertSorted(ObservableCollection<ExplorerNodeViewModel> siblings, ExplorerNodeViewModel node)
    {
        var index = 0;
        while (index < siblings.Count && Compare(siblings[index], node) < 0)
        {
            index++;
        }

        siblings.Insert(index, node);
    }

    private static int Compare(ExplorerNodeViewModel a, ExplorerNodeViewModel b) =>
        a.IsFolder != b.IsFolder ? (a.IsFolder ? -1 : 1) : string.CompareOrdinal(a.Name, b.Name);

    private static string Plural(int count) => count == 1 ? "1 test" : $"{count} tests";
}
