// The form of one target in the targets editor: its frame and within, picked
// from the other targets, and its candidates in order of reliability, which
// can be added, removed and reordered. Every valid change rewrites the
// target's lines at once.

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Desktop.App.StepFiles;
using Desktop.Protocol.Messages;

namespace Desktop.App.ViewModels.Targets;

/// <summary>The form of one target.</summary>
public sealed partial class TargetFormViewModel : ObservableObject
{
    private readonly string _text;
    private readonly Action<string> _apply;
    private readonly string? _frameRaw;
    private readonly string? _withinRaw;
    private readonly bool _loading = true;

    /// <summary>Creates the form.</summary>
    /// <param name="outline">The file's targets.</param>
    /// <param name="target">The target.</param>
    /// <param name="others">The names of the other targets, for frame and within.</param>
    /// <param name="apply">Puts the target's new text (without indentation) in place of its lines.</param>
    public TargetFormViewModel(TargetsOutline outline, TargetOutline target, IReadOnlyList<string> others, Action<string> apply)
    {
        ArgumentNullException.ThrowIfNull(outline);
        ArgumentNullException.ThrowIfNull(target);
        _text = outline.Text;
        _apply = apply;
        Name = target.Name;
        Problem = target.Problem is { } problem ? $"The editor cannot show this target. {problem} Edit it as text." : null;
        FrameChoices = [string.Empty, .. others.Where(o => o != target.Name)];
        _frameRaw = target.Frame is null ? null : StepWriter.Raw(_text, target.Frame);
        _withinRaw = target.Within is null ? null : StepWriter.Raw(_text, target.Within);
        IsFrameInline = target.Frame is not (null or YamlScalar);
        IsWithinInline = target.Within is not (null or YamlScalar);
        Frame = target.Frame is YamlScalar frame ? frame.Value : string.Empty;
        Within = target.Within is YamlScalar within ? within.Value : string.Empty;
        var texts = TargetWriter.CandidateTexts(outline, target);
        for (var i = 0; i < target.Candidates.Count; i++)
        {
            Candidates.Add(new CandidateViewModel(target.Candidates[i], texts[i], Changed));
        }

        _loading = false;
    }

    /// <summary>The target's name.</summary>
    public string Name { get; }

    /// <summary>Why the target can only be edited as text; null when the form can show it.</summary>
    public string? Problem { get; }

    /// <summary>True when the form can edit the target.</summary>
    public bool IsEditable => Problem is null;

    /// <summary>The targets frame and within can name, after an empty choice for none.</summary>
    public IReadOnlyList<string> FrameChoices { get; }

    /// <summary>True when the frame is written inline; it is kept as written.</summary>
    public bool IsFrameInline { get; }

    /// <summary>True when within is written inline; it is kept as written.</summary>
    public bool IsWithinInline { get; }

    /// <summary>The target whose iframe holds this one; empty for none.</summary>
    [ObservableProperty]
    private string _frame;

    /// <summary>The target this one is inside; empty for none.</summary>
    [ObservableProperty]
    private string _within;

    /// <summary>Why frame and within cannot be written as they are; null when they can.</summary>
    [ObservableProperty]
    private string? _error;

    /// <summary>The candidates, in order of reliability.</summary>
    public ObservableCollection<CandidateViewModel> Candidates { get; } = [];

    /// <summary>Shows beside each candidate how many elements it matched in a failure.</summary>
    /// <param name="counts">The failure's candidates and their match counts.</param>
    public void ShowCounts(IReadOnlyList<CandidateMatches> counts)
    {
        ArgumentNullException.ThrowIfNull(counts);
        foreach (var candidate in Candidates)
        {
            var count = counts.FirstOrDefault(c => candidate.Matches(c.Candidate));
            candidate.MatchText = count is null ? null : count.Matches switch
            {
                0 => "matched no element",
                1 => "matched 1 element",
                var n => $"matched {n} elements",
            };
        }
    }

    /// <summary>The target's text as the form gives it.</summary>
    /// <returns>The text without indentation.</returns>
    /// <exception cref="FormatException">A field does not fit.</exception>
    public string Write()
    {
        if (Frame.Length > 0 && Within.Length > 0)
        {
            throw new FormatException("A target has a frame or a within, not both: put the frame on the outer target.");
        }

        var frame = IsFrameInline ? _frameRaw : Frame.Length == 0 ? null : StepWriter.Text(Frame);
        var within = IsWithinInline ? _withinRaw : Within.Length == 0 ? null : StepWriter.Text(Within);
        if (frame?.Contains('\n') == true || within?.Contains('\n') == true)
        {
            throw new FormatException("An inline frame or within spans several lines; edit this target as text.");
        }

        return TargetWriter.Write(Name, frame, within, [.. Candidates.Select(c => c.ToValue())]);
    }

    /// <summary>Adds a test ID candidate at the end.</summary>
    [RelayCommand(CanExecute = nameof(IsEditable))]
    private void AddCandidate()
    {
        Candidates.Add(new CandidateViewModel(null, null, Changed));
        Changed();
    }

    /// <summary>Removes a candidate.</summary>
    /// <param name="candidate">The candidate.</param>
    [RelayCommand]
    private void RemoveCandidate(CandidateViewModel? candidate)
    {
        if (candidate is not null && Candidates.Remove(candidate))
        {
            Changed();
        }
    }

    /// <summary>Moves a candidate up: more reliable, tried earlier.</summary>
    /// <param name="candidate">The candidate.</param>
    [RelayCommand]
    private void MoveCandidateUp(CandidateViewModel? candidate) => MoveCandidate(candidate, -1);

    /// <summary>Moves a candidate down: less reliable, tried later.</summary>
    /// <param name="candidate">The candidate.</param>
    [RelayCommand]
    private void MoveCandidateDown(CandidateViewModel? candidate) => MoveCandidate(candidate, +1);

    partial void OnFrameChanged(string value) => Changed();

    partial void OnWithinChanged(string value) => Changed();

    private void MoveCandidate(CandidateViewModel? candidate, int delta)
    {
        var index = candidate is null ? -1 : Candidates.IndexOf(candidate);
        if (index < 0 || index + delta < 0 || index + delta >= Candidates.Count)
        {
            return;
        }

        Candidates.Move(index, index + delta);
        Changed();
    }

    private void Changed()
    {
        if (_loading || !IsEditable)
        {
            return;
        }

        string text;
        try
        {
            text = Write();
            Error = null;
        }
        catch (FormatException ex)
        {
            Error = Candidates.Any(c => c.Error is not null) ? null : ex.Message;
            return;
        }

        _apply(text);
    }
}
