// The earlier runs of a project, newest first, read from their run folders,
// and opening one in the same run view as a live run.

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Desktop.App.Services;
using Desktop.Protocol.Messages;

namespace Desktop.App.ViewModels.Runs;

/// <summary>One earlier run in the list.</summary>
/// <param name="Summary">What its events say.</param>
/// <param name="IsLive">True for the run going on now.</param>
public sealed record RunHistoryItemViewModel(RunRecordSummary Summary, bool IsLive)
{
    /// <summary>The run's id.</summary>
    public string RunId => Summary.RunId;

    /// <summary>The start time, local, such as "2026-10-10 09:15:00"; the run id when unknown.</summary>
    public string StartedText => Summary.StartedAt is { } t
        ? t.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture)
        : Summary.RunId;

    /// <summary>The environment, or empty.</summary>
    public string Environment => Summary.Environment ?? string.Empty;

    /// <summary>How it ended, in a word or two.</summary>
    public string StatusText => IsLive
        ? "Running…"
        : Summary.Status switch
        {
            RunOutcome.Passed => "Passed",
            RunOutcome.Failed => "Failed",
            RunOutcome.Cancelled => "Cancelled",
            _ => "Did not finish",
        };

    /// <summary>One character for the result, as in the run view.</summary>
    public string Glyph => IsLive ? RunGlyphs.Of("Running") : RunGlyphs.Of(Summary.Status?.ToString() ?? "Interrupted");

    /// <summary>"3 passed, 1 failed, 0 cancelled, 1 skipped", or empty.</summary>
    public string TotalsText => Summary.Totals is { } t
        ? $"{t.Passed} passed, {t.Failed} failed, {t.Cancelled} cancelled, {t.Skipped} skipped"
        : string.Empty;

    /// <summary>What is wrong with its record, or null.</summary>
    public string? Problem => Summary.Problem;
}

/// <summary>What the history needs from its workspace.</summary>
/// <param name="Root">The project's root folder.</param>
/// <param name="LiveRunId">The id of the run going on now, if any.</param>
/// <param name="TrySelectOpen">Selects the tab already showing a run; false when none does.</param>
/// <param name="ShowRun">Opens a run read back from its record in a tab.</param>
/// <param name="Report">Writes a line into the engine log.</param>
public sealed record RunHistoryHost(
    string Root,
    Func<string?> LiveRunId,
    Func<string, bool> TrySelectOpen,
    Action<RunViewModel> ShowRun,
    Action<string> Report);

/// <summary>The list of earlier runs.</summary>
public sealed partial class RunHistoryViewModel : ObservableObject
{
    private readonly IRunRecords _records;
    private readonly RunHistoryHost _host;
    private int _generation;

    /// <summary>Creates the list; call <see cref="RefreshAsync"/> to fill it.</summary>
    /// <param name="records">Reads run folders.</param>
    /// <param name="host">What it needs from the workspace.</param>
    public RunHistoryViewModel(IRunRecords records, RunHistoryHost host)
    {
        _records = records;
        _host = host;
    }

    /// <summary>The runs, newest first.</summary>
    public ObservableCollection<RunHistoryItemViewModel> Items { get; } = [];

    /// <summary>"12 runs", or a note that there are none.</summary>
    [ObservableProperty]
    private string _summary = "No runs yet";

    /// <summary>What happened when a run could not be opened; null otherwise.</summary>
    [ObservableProperty]
    private string? _notice;

    /// <summary>Reads the run folders again. Several refreshes at once apply only the newest.</summary>
    /// <returns>A task that completes when the list is up to date.</returns>
    [RelayCommand]
    public async Task RefreshAsync()
    {
        var generation = ++_generation;
        IReadOnlyList<RunRecordSummary> runs;
        try
        {
            runs = await Task.Run(() => _records.List(_host.Root));
        }
        catch (Exception ex)
        {
            _host.Report($"Listing earlier runs failed: {ex}");
            runs = [];
        }

        if (generation != _generation)
        {
            return;
        }

        var live = _host.LiveRunId();
        Items.Clear();
        foreach (var run in runs)
        {
            Items.Add(new RunHistoryItemViewModel(run, run.RunId == live));
        }

        Summary = runs.Count switch
        {
            0 => "No runs yet",
            1 => "1 run",
            _ => $"{runs.Count} runs",
        };
    }

    /// <summary>Opens a run in a tab, or selects its tab when it is already open.</summary>
    /// <param name="item">The run.</param>
    /// <returns>A task that completes when the run is shown, or the reason it cannot be is.</returns>
    [RelayCommand]
    private async Task OpenAsync(RunHistoryItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        Notice = null;
        if (_host.TrySelectOpen(item.RunId))
        {
            return;
        }

        RunRecord? record;
        try
        {
            record = await Task.Run(() => _records.Read(_host.Root, item.RunId));
        }
        catch (Exception ex)
        {
            Notice = $"Run {item.StartedText} cannot be read: {ex.Message}";
            _host.Report($"Reading run {item.RunId} failed: {ex}");
            return;
        }

        if (record is null)
        {
            Notice = $"Run {item.StartedText} is gone: the engine deletes the oldest runs when it starts a new one.";
            Items.Remove(item);
            return;
        }

        _host.ShowRun(Build(record));
    }

    /// <summary>Builds a run from its record, as a live run is built from its events.</summary>
    /// <param name="record">The run's events.</param>
    /// <returns>The run; one whose events end without <c>runFinished</c> is shown as not finished.</returns>
    public static RunViewModel Build(RunRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        var run = new RunViewModel(record.RunId, record.Folder);
        foreach (var engineEvent in record.Events)
        {
            run.Apply(engineEvent);
        }

        if (record.DamagedLines > 0)
        {
            run.Messages.Add($"{record.DamagedLines} {(record.DamagedLines == 1 ? "line" : "lines")} of this run's events could not be read and are left out; what is shown may be incomplete.");
        }

        run.EndUnfinished("The record of this run ends before the run finished: the engine stopped, or the run is still going in another window.");
        return run;
    }
}
