// One test instance of a run (a test file, or one data row of it): its status,
// and its steps grouped by section, in the order they started.

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Desktop.Protocol.Messages;

namespace Desktop.App.ViewModels.Runs;

/// <summary>Where a test instance of a run stands.</summary>
public enum TestRunState
{
    /// <summary>Announced, not started yet.</summary>
    Pending,
    /// <summary>Running.</summary>
    Running,
    /// <summary>Passed.</summary>
    Passed,
    /// <summary>Failed.</summary>
    Failed,
    /// <summary>Cancelled.</summary>
    Cancelled,
    /// <summary>Skipped, with its reason.</summary>
    Skipped,
    /// <summary>Never finished: the engine stopped, or the run's record ends.</summary>
    Interrupted,
}

/// <summary>The steps of one section of a test instance.</summary>
/// <param name="Section">The section.</param>
public sealed record StepSectionViewModel(StepSection Section)
{
    /// <summary>"before", "steps" or "after".</summary>
    public string Name => Section.ToString().ToLowerInvariant();

    /// <summary>The section's steps, in the order they started; steps of called flows follow their calling step.</summary>
    public ObservableCollection<StepRunViewModel> Steps { get; } = [];
}

/// <summary>One test instance of a run.</summary>
public sealed partial class TestRunViewModel : ObservableObject
{
    private readonly Dictionary<string, StepRunViewModel> _steps = new(StringComparer.Ordinal);

    /// <summary>Creates the instance as <c>runStarted</c> announced it.</summary>
    /// <param name="planned">The announcement.</param>
    public TestRunViewModel(PlannedTest planned)
    {
        ArgumentNullException.ThrowIfNull(planned);
        Planned = planned;
        if (planned.Skip is { } reason)
        {
            SkipReason = reason;
        }
    }

    /// <summary>What <c>runStarted</c> said about it.</summary>
    public PlannedTest Planned { get; }

    /// <summary>The instance id: file plus <c>#</c> and the row index.</summary>
    public string TestId => Planned.TestId;

    /// <summary>The test file.</summary>
    public string File => Planned.File;

    /// <summary>The test's name, row values filled in.</summary>
    public string Name => Planned.Name;

    /// <summary>"row 2" for a data row, else empty.</summary>
    public string RowText => Planned.Row is { } row ? $"row {row + 1}" : string.Empty;

    /// <summary>The sections that have steps, in the order before, steps, after.</summary>
    public ObservableCollection<StepSectionViewModel> Sections { get; } = [];

    /// <summary>Warnings and log lines about the test that belong to no step.</summary>
    public ObservableCollection<string> Messages { get; } = [];

    /// <summary>Where it stands.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText), nameof(Glyph))]
    private TestRunState _state = TestRunState.Pending;

    /// <summary>One character for the state, as for steps; ○ pending, ⊘ cancelled.</summary>
    public string Glyph => RunGlyphs.Of(State.ToString());

    /// <summary>How long it took, in milliseconds, once finished.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DurationText))]
    private double? _durationMs;

    /// <summary>Why it was skipped (its <c>skip</c> text).</summary>
    [ObservableProperty]
    private string? _skipReason;

    /// <summary>The selected step, whose details are shown.</summary>
    [ObservableProperty]
    private StepRunViewModel? _selectedStep;

    /// <summary>Shows a step's details.</summary>
    /// <param name="step">The step.</param>
    [RelayCommand]
    private void SelectStep(StepRunViewModel? step) => SelectedStep = step;

    partial void OnSelectedStepChanged(StepRunViewModel? oldValue, StepRunViewModel? newValue)
    {
        if (oldValue is not null)
        {
            oldValue.IsSelected = false;
        }

        if (newValue is not null)
        {
            newValue.IsSelected = true;
        }
    }

    /// <summary>Every step, in the order they started.</summary>
    public IEnumerable<StepRunViewModel> AllSteps => Sections.SelectMany(s => s.Steps);

    /// <summary>The state in a word.</summary>
    public string StatusText => State.ToString().ToLowerInvariant();

    /// <summary>"4.2 s", or empty.</summary>
    public string DurationText => DurationMs is { } ms ? (ms >= 1000 ? $"{ms / 1000:0.0} s" : $"{ms:0} ms") : string.Empty;

    /// <summary>Finds a step by id.</summary>
    /// <param name="stepId">The step's id.</param>
    /// <returns>The step, or null.</returns>
    public StepRunViewModel? Step(string stepId) => _steps.GetValueOrDefault(stepId);

    /// <summary>Adds a step that started.</summary>
    /// <param name="started">The event.</param>
    /// <returns>The step.</returns>
    public StepRunViewModel Add(StepStartedEvent started)
    {
        ArgumentNullException.ThrowIfNull(started);
        var depth = started.ParentStepId is { } parent && _steps.TryGetValue(parent, out var caller) ? caller.Depth + 1 : 0;
        var step = new StepRunViewModel(started, depth);
        _steps[started.StepId] = step;
        var section = Sections.FirstOrDefault(s => s.Section == started.Section);
        if (section is null)
        {
            section = new StepSectionViewModel(started.Section);
            var index = 0;
            while (index < Sections.Count && Sections[index].Section < started.Section)
            {
                index++;
            }

            Sections.Insert(index, section);
        }

        section.Steps.Add(step);
        return step;
    }

    /// <summary>Marks the instance and its running steps as interrupted, when the run ends without finishing it.</summary>
    public void Interrupt()
    {
        foreach (var step in AllSteps)
        {
            step.Interrupt();
        }

        if (State is TestRunState.Pending or TestRunState.Running)
        {
            State = TestRunState.Interrupted;
        }
    }
}
