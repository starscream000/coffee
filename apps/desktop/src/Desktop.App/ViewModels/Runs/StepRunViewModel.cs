// One step of a test instance in a run, as the events report it: started,
// then passed, failed or skipped, with its error, the targets it resolved, its
// warnings, its screenshot and whether its page state was saved.

using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using Desktop.Protocol.Messages;

namespace Desktop.App.ViewModels.Runs;

/// <summary>Where a step of a run stands.</summary>
public enum StepRunState
{
    /// <summary>Started, no result yet.</summary>
    Running,
    /// <summary>Passed.</summary>
    Passed,
    /// <summary>Failed.</summary>
    Failed,
    /// <summary>Did not run.</summary>
    Skipped,
    /// <summary>Started, but the run ended without a result for it (the engine stopped, or the run's record ends).</summary>
    Interrupted,
}

/// <summary>One locator candidate tried for a target that was not found, with how many elements it matched.</summary>
/// <param name="Candidate">The candidate as JSON, such as <c>{"testId":"cart-count"}</c>.</param>
/// <param name="Matches">How many elements it matched.</param>
public sealed record CandidateRow(string Candidate, int Matches);

/// <summary>How one target of the step was found.</summary>
/// <param name="Description">"target cart.count: candidate 2 {"css":".count"}", nested frame and within levels included.</param>
/// <param name="IsFallback">True when a candidate other than the first was used, at any level.</param>
/// <param name="NotFound">True when no candidate matched.</param>
/// <param name="Target">The named target to look at: the level where the search stopped, or where a fallback was used; null when that level is inline or all is well.</param>
public sealed record LocatorRow(string Description, bool IsFallback, bool NotFound, string? Target = null)
{
    /// <summary>True when the row links to a target in the editor.</summary>
    public bool HasTarget => Target is not null;

    /// <summary>"Open target cart.count".</summary>
    public string OpenText => $"Open target {Target}";
}

/// <summary>One step of a test instance in a run.</summary>
public sealed partial class StepRunViewModel : ObservableObject
{
    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    /// <summary>Creates the step from its <c>stepStarted</c> event.</summary>
    /// <param name="started">The event.</param>
    /// <param name="depth">How deep it is in called flows: 0 for a step of the test itself.</param>
    public StepRunViewModel(StepStartedEvent started, int depth)
    {
        ArgumentNullException.ThrowIfNull(started);
        Started = started;
        Depth = depth;
        ParamsText = JsonSerializer.Serialize(started.Params, Indented);
    }

    /// <summary>The <c>stepStarted</c> event.</summary>
    public StepStartedEvent Started { get; }

    /// <summary>The step's id, such as <c>steps.3</c> or <c>steps.4/steps.1</c>.</summary>
    public string StepId => Started.StepId;

    /// <summary>How deep it is in called flows: 0 for a step of the test itself.</summary>
    public int Depth { get; }

    /// <summary>Left indent for the view, from <see cref="Depth"/>.</summary>
    public Avalonia.Thickness Indent => new(Depth * 16, 0, 0, 0);

    /// <summary>The section: before, steps or after.</summary>
    public StepSection Section => Started.Section;

    /// <summary>The action's name.</summary>
    public string Action => Started.Action;

    /// <summary>A short description, for people.</summary>
    public string Title => Started.Title;

    /// <summary>The page the step acted on.</summary>
    public string Page => Started.Page;

    /// <summary>The step's place in its file.</summary>
    public Location Location => Started.Location;

    /// <summary>"tests/a.test.yaml:12".</summary>
    public string LocationText => $"{Location.File}:{Location.Line}";

    /// <summary>The step's parameters (variables not filled in) as indented JSON.</summary>
    public string ParamsText { get; }

    /// <summary>Warnings and other log lines about this step, such as a locator fallback.</summary>
    public ObservableCollection<string> Warnings { get; } = [];

    /// <summary>The targets the step resolved.</summary>
    public ObservableCollection<LocatorRow> Locators { get; } = [];

    /// <summary>For a target that was not found: each candidate tried and its match count.</summary>
    public ObservableCollection<CandidateRow> Candidates { get; } = [];

    /// <summary>Where the step stands.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsFailed), nameof(StatusText), nameof(Glyph))]
    private StepRunState _state = StepRunState.Running;

    /// <summary>True when the step is the one whose details are shown.</summary>
    [ObservableProperty]
    private bool _isSelected;

    /// <summary>One character for the state: ✓ passed, ✗ failed, – skipped, … running, ! interrupted.</summary>
    public string Glyph => RunGlyphs.Of(State.ToString());

    /// <summary>How long it took, in milliseconds, once finished.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DurationText))]
    private double? _durationMs;

    /// <summary>Why it failed.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ExpectedText), nameof(ActualText), nameof(HasExpectedOrActual))]
    private ErrorInfo? _error;

    /// <summary>Why it was skipped, for people.</summary>
    [ObservableProperty]
    private string? _skipMessage;

    /// <summary>Absolute path of the step's screenshot, once <c>screenshotReady</c> arrived.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasScreenshot))]
    private string? _screenshotPath;

    /// <summary>True once the step's page state was saved (<c>snapshotReady</c>, or a result saying so).</summary>
    [ObservableProperty]
    private bool _hasPageState;

    /// <summary>Why the page state could not be recorded, when it failed.</summary>
    [ObservableProperty]
    private string? _pageStateProblem;

    /// <summary>True when the failure listed locator candidates.</summary>
    public bool HasCandidates => Candidates.Count > 0;

    /// <summary>True when the step failed.</summary>
    public bool IsFailed => State == StepRunState.Failed;

    /// <summary>True when a screenshot was recorded.</summary>
    public bool HasScreenshot => ScreenshotPath is not null;

    /// <summary>True when the error has an expected or an actual value.</summary>
    public bool HasExpectedOrActual => Error?.Expected is not null || Error?.Actual is not null;

    /// <summary>The expected value as text.</summary>
    public string? ExpectedText => Error?.Expected is { } e ? Describe(e) : null;

    /// <summary>The actual value as text.</summary>
    public string? ActualText => Error?.Actual is { } a ? Describe(a) : null;

    /// <summary>"1.2 s", "340 ms", or empty while running.</summary>
    public string DurationText => DurationMs is { } ms ? (ms >= 1000 ? $"{ms / 1000:0.0} s" : $"{ms:0} ms") : string.Empty;

    /// <summary>The state in a word.</summary>
    public string StatusText => State switch
    {
        StepRunState.Running => "running",
        StepRunState.Passed => "passed",
        StepRunState.Failed => "failed",
        StepRunState.Skipped => "skipped",
        _ => "interrupted",
    };

    /// <summary>Applies the step's result.</summary>
    /// <param name="passed">The event.</param>
    public void Apply(StepPassedEvent passed)
    {
        ArgumentNullException.ThrowIfNull(passed);
        State = StepRunState.Passed;
        DurationMs = passed.DurationMs;
        SetLocators(passed.Locators);
        SetSnapshot(passed.Snapshot);
    }

    /// <summary>Applies the step's failure.</summary>
    /// <param name="failed">The event.</param>
    public void Apply(StepFailedEvent failed)
    {
        ArgumentNullException.ThrowIfNull(failed);
        State = StepRunState.Failed;
        DurationMs = failed.DurationMs;
        Error = failed.Error;
        Candidates.Clear();
        foreach (var c in failed.Error.Candidates ?? [])
        {
            Candidates.Add(new CandidateRow(JsonSerializer.Serialize(c.Candidate), c.Matches));
        }

        OnPropertyChanged(nameof(HasCandidates));
        SetLocators(failed.Locators);
        SetSnapshot(failed.Snapshot);
    }

    /// <summary>Marks the step skipped.</summary>
    /// <param name="skipped">The event.</param>
    public void Apply(StepSkippedEvent skipped)
    {
        ArgumentNullException.ThrowIfNull(skipped);
        State = StepRunState.Skipped;
        SkipMessage = skipped.Message;
    }

    /// <summary>Marks a step that never got its result.</summary>
    public void Interrupt()
    {
        if (State == StepRunState.Running)
        {
            State = StepRunState.Interrupted;
        }
    }

    private void SetLocators(IReadOnlyList<LocatorUse> uses)
    {
        Locators.Clear();
        foreach (var use in uses)
        {
            Locators.Add(new LocatorRow(DescribeUse(use), IsFallback(use), NotFound(use), Concern(use)?.Target));
        }
    }

    private void SetSnapshot(SnapshotStatus snapshot)
    {
        HasPageState |= snapshot.State == SnapshotState.Saved;
        PageStateProblem = snapshot.State == SnapshotState.Failed ? snapshot.Reason : null;
    }

    /// <summary>True when no candidate matched at some level (the search stops there).</summary>
    private static bool NotFound(LocatorUse use) =>
        use.CandidateIndex is null || (use.Frame is { } f && NotFound(f)) || (use.Within is { } w && NotFound(w));

    /// <summary>
    /// The level to look at: the outermost level where nothing matched (frames are
    /// found first, then within, then the element), else the outermost level where
    /// a fallback was used; null when every level used its first candidate.
    /// </summary>
    private static LocatorUse? Concern(LocatorUse use)
    {
        IEnumerable<LocatorUse> Levels(LocatorUse u) =>
            (u.Frame is { } f ? Levels(f) : []).Concat(u.Within is { } w ? Levels(w) : []).Append(u);
        var levels = Levels(use).ToList();
        return levels.FirstOrDefault(l => l.CandidateIndex is null) ?? levels.FirstOrDefault(l => l.CandidateIndex > 0);
    }

    private static bool IsFallback(LocatorUse use) =>
        use.CandidateIndex > 0 || (use.Frame is { } f && IsFallback(f)) || (use.Within is { } w && IsFallback(w));

    private static string DescribeUse(LocatorUse use)
    {
        var name = use.Target is { } t ? $"{use.Param} {t}" : $"{use.Param} (inline)";
        var how = use.CandidateIndex is { } i
            ? $"candidate {i + 1} {(use.Candidate is { } c ? JsonSerializer.Serialize(c) : string.Empty)}"
            : "not found";
        var nested = string.Concat(
            use.Frame is { } frame ? $"; in frame: {DescribeUse(frame)}" : string.Empty,
            use.Within is { } within ? $"; within: {DescribeUse(within)}" : string.Empty);
        return $"{name}: {how}{nested}";
    }

    private static string Describe(JsonElement value) =>
        value.ValueKind == JsonValueKind.String ? $"\"{value.GetString()}\"" : value.GetRawText();
}
