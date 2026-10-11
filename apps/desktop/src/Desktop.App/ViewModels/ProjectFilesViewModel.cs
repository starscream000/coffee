// The project panel's groups besides the tests (instruction D0004, task 18):
// the flows, the shared targets files and the config file, found on disk
// under the project root. Selecting one opens it; the workspace offers new,
// rename and delete for each group.

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Desktop.Protocol;

namespace Desktop.App.ViewModels;

/// <summary>A file in one of the project panel's groups.</summary>
/// <param name="Path">Path relative to the project root, with forward slashes.</param>
public sealed record ProjectFileViewModel(string Path)
{
    /// <summary>The file's name.</summary>
    public string Name => Path[(Path.LastIndexOf('/') + 1)..];

    /// <summary>The folder it is in, relative to the project root; empty at the root.</summary>
    public string Folder => Path.LastIndexOf('/') is var slash and > 0 ? Path[..slash] : string.Empty;
}

/// <summary>The flows, the shared targets files and the config file of a project.</summary>
public sealed partial class ProjectFilesViewModel : ObservableObject
{
    private bool _clearing;

    /// <summary>Raised when the person picks a file to look at.</summary>
    public event EventHandler<string>? FileActivated;

    /// <summary>The flows, sorted by path.</summary>
    public ObservableCollection<ProjectFileViewModel> Flows { get; } = [];

    /// <summary>The shared targets files, sorted by path.</summary>
    public ObservableCollection<ProjectFileViewModel> TargetsFiles { get; } = [];

    /// <summary>The config file.</summary>
    public ProjectFileViewModel Config { get; } = new(Product.ConfigFile);

    /// <summary>"2 flows", or nothing when there are none.</summary>
    public string FlowsSummary => Count(Flows.Count, "flow");

    /// <summary>"1 file", or nothing when there are none.</summary>
    public string TargetsSummary => Count(TargetsFiles.Count, "file");

    /// <summary>True when the project has no flow.</summary>
    public bool HasNoFlows => Flows.Count == 0;

    /// <summary>True when the project has no shared targets file.</summary>
    public bool HasNoTargetsFiles => TargetsFiles.Count == 0;

    /// <summary>The selected flow.</summary>
    [ObservableProperty]
    private ProjectFileViewModel? _selectedFlow;

    /// <summary>The selected targets file.</summary>
    [ObservableProperty]
    private ProjectFileViewModel? _selectedTargetsFile;

    /// <summary>Shows the files found on disk, keeping the selection where the file is still there.</summary>
    /// <param name="flows">The flows, relative to the project root.</param>
    /// <param name="targetsFiles">The shared targets files, relative to the project root.</param>
    public void Load(IReadOnlyList<string> flows, IReadOnlyList<string> targetsFiles)
    {
        ArgumentNullException.ThrowIfNull(flows);
        ArgumentNullException.ThrowIfNull(targetsFiles);
        var flow = SelectedFlow?.Path;
        var targets = SelectedTargetsFile?.Path;
        _clearing = true;
        try
        {
            Fill(Flows, flows);
            Fill(TargetsFiles, targetsFiles);
            SelectedFlow = Flows.FirstOrDefault(f => f.Path == flow);
            SelectedTargetsFile = TargetsFiles.FirstOrDefault(f => f.Path == targets);
        }
        finally
        {
            _clearing = false;
        }

        OnPropertyChanged(nameof(FlowsSummary));
        OnPropertyChanged(nameof(TargetsSummary));
        OnPropertyChanged(nameof(HasNoFlows));
        OnPropertyChanged(nameof(HasNoTargetsFiles));
    }

    /// <summary>Clears the selection of every group here, without opening anything.</summary>
    public void ClearSelection()
    {
        _clearing = true;
        try
        {
            SelectedFlow = null;
            SelectedTargetsFile = null;
        }
        finally
        {
            _clearing = false;
        }
    }

    /// <summary>Opens the config file.</summary>
    [RelayCommand]
    private void OpenConfig()
    {
        ClearSelection();
        FileActivated?.Invoke(this, Config.Path);
    }

    partial void OnSelectedFlowChanged(ProjectFileViewModel? value) => Activate(value, () => SelectedTargetsFile = null);

    partial void OnSelectedTargetsFileChanged(ProjectFileViewModel? value) => Activate(value, () => SelectedFlow = null);

    private void Activate(ProjectFileViewModel? file, Action clearOther)
    {
        if (_clearing || file is null)
        {
            return;
        }

        _clearing = true;
        try
        {
            clearOther();
        }
        finally
        {
            _clearing = false;
        }

        FileActivated?.Invoke(this, file.Path);
    }

    private static void Fill(ObservableCollection<ProjectFileViewModel> list, IReadOnlyList<string> paths)
    {
        list.Clear();
        foreach (var path in paths.Order(StringComparer.Ordinal))
        {
            list.Add(new ProjectFileViewModel(path));
        }
    }

    private static string Count(int n, string what) => n switch
    {
        0 => string.Empty,
        1 => $"1 {what}",
        _ => $"{n} {what}s",
    };
}
