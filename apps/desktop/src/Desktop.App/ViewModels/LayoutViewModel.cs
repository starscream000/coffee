// The window's layout that the app remembers: the width of the project panel,
// the height of the bottom panel, and whether the bottom panel is open
// (instruction D0004, task 8). The workspace view
// applies it and reports the sizes a person drags; the shell saves it.

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Desktop.App.Services;

namespace Desktop.App.ViewModels;

/// <summary>The parts of the bottom panel.</summary>
public enum BottomPanelView
{
    /// <summary>The problems of the project's files.</summary>
    Problems,
    /// <summary>The project's runs.</summary>
    Runs,
    /// <summary>The engine's log.</summary>
    EngineLog,
}

/// <summary>Panel sizes and open states, remembered between sessions.</summary>
public sealed partial class LayoutViewModel : ObservableObject
{
    /// <summary>The smallest width or height a panel is given back, so a dragged-away panel can be found again.</summary>
    public const double MinimumSize = 120;

    /// <summary>Creates the layout from saved settings.</summary>
    /// <param name="saved">What was saved, or the defaults.</param>
    public LayoutViewModel(LayoutSettings saved)
    {
        ArgumentNullException.ThrowIfNull(saved);
        _projectWidth = Math.Max(saved.ProjectWidth, MinimumSize);
        _bottomHeight = Math.Max(saved.BottomHeight, MinimumSize);
        _isBottomOpen = saved.IsBottomOpen;
    }

    /// <summary>Raised when something worth saving changed.</summary>
    public event EventHandler? Changed;

    /// <summary>Width of the project panel, in pixels.</summary>
    [ObservableProperty]
    private double _projectWidth;

    /// <summary>Height of the bottom panel (problems, runs, engine log) when open, in pixels.</summary>
    [ObservableProperty]
    private double _bottomHeight;

    /// <summary>True when the bottom panel is open.</summary>
    [ObservableProperty]
    private bool _isBottomOpen;

    /// <summary>The part the bottom panel shows; not remembered.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsProblemsShown), nameof(IsRunsShown), nameof(IsEngineLogShown))]
    private BottomPanelView _bottomView;

    /// <summary>True when the bottom panel shows the problems.</summary>
    public bool IsProblemsShown => BottomView == BottomPanelView.Problems;

    /// <summary>True when the bottom panel shows the runs.</summary>
    public bool IsRunsShown => BottomView == BottomPanelView.Runs;

    /// <summary>True when the bottom panel shows the engine log.</summary>
    public bool IsEngineLogShown => BottomView == BottomPanelView.EngineLog;

    /// <summary>The layout as it is saved.</summary>
    public LayoutSettings Settings => new()
    {
        ProjectWidth = ProjectWidth,
        BottomHeight = BottomHeight,
        IsBottomOpen = IsBottomOpen,
    };

    /// <summary>Takes the sizes a person dragged the splitters to.</summary>
    /// <param name="projectWidth">The project panel's width.</param>
    /// <param name="bottomHeight">The bottom panel's height, when open.</param>
    public void Resized(double projectWidth, double? bottomHeight)
    {
        ProjectWidth = Math.Max(projectWidth, MinimumSize);
        if (bottomHeight is { } b && IsBottomOpen)
        {
            BottomHeight = Math.Max(b, MinimumSize);
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Opens or closes the bottom panel.</summary>
    [RelayCommand]
    private void ToggleBottom() => IsBottomOpen = !IsBottomOpen;

    /// <summary>Shows the problems in the bottom panel, opening it.</summary>
    [RelayCommand]
    private void ShowProblems() => ShowBottom(BottomPanelView.Problems);

    /// <summary>Shows the runs in the bottom panel, opening it.</summary>
    [RelayCommand]
    private void ShowRuns() => ShowBottom(BottomPanelView.Runs);

    /// <summary>Shows the engine log in the bottom panel, opening it.</summary>
    [RelayCommand]
    private void ShowEngineLog() => ShowBottom(BottomPanelView.EngineLog);

    private void ShowBottom(BottomPanelView view)
    {
        BottomView = view;
        IsBottomOpen = true;
    }

    partial void OnIsBottomOpenChanged(bool value) => Changed?.Invoke(this, EventArgs.Empty);
}
