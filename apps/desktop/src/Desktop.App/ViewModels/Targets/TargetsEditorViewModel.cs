// The targets editor of a test, flow or targets file: the file's targets by
// name, the selected target's form, and adding and removing targets. Like the
// step list, it reads the document again on every change and applies its own
// changes to the document as one replacement of the part that differs.

using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using AvaloniaEdit.Document;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Desktop.App.StepFiles;
using Desktop.Protocol.Messages;

namespace Desktop.App.ViewModels.Targets;

/// <summary>The targets editor of a file.</summary>
public sealed partial class TargetsEditorViewModel : ObservableObject
{
    private readonly TextDocument _document;
    private readonly Func<IReadOnlyList<string>> _sharedTargets;
    private IReadOnlyList<CandidateMatches>? _counts;
    private bool _editing;

    /// <summary>Creates the editor and reads the document.</summary>
    /// <param name="document">The text, shared with the text editor.</param>
    /// <param name="sharedTargets">The names of the shared targets, for frame and within.</param>
    public TargetsEditorViewModel(TextDocument document, Func<IReadOnlyList<string>> sharedTargets)
    {
        ArgumentNullException.ThrowIfNull(document);
        _document = document;
        _sharedTargets = sharedTargets;
        Outline = TargetsOutline.Read(string.Empty);
        _document.TextChanged += (_, _) =>
        {
            if (!_editing)
            {
                Refresh();
            }
        };
        Refresh();
    }

    /// <summary>The file's targets as last read.</summary>
    public TargetsOutline Outline { get; private set; }

    /// <summary>The targets' names, in the file's order.</summary>
    public ObservableCollection<string> Names { get; } = [];

    /// <summary>True when the targets can be shown.</summary>
    public bool IsAvailable => Outline.IsReadable;

    /// <summary>Why the targets are shown as text only; null when they can be shown.</summary>
    public string? Problem => Outline.IsReadable ? null : $"The targets editor cannot show this file. {Outline.Problem} Edit it as text.";

    /// <summary>"No targets yet" when the file has none; null otherwise.</summary>
    public string? EmptyText => Outline.IsReadable && Outline.Targets.Count == 0 ? "No targets yet." : null;

    /// <summary>The name of the selected target.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RemoveTargetCommand))]
    private string? _selectedName;

    /// <summary>The selected target's form.</summary>
    [ObservableProperty]
    private TargetFormViewModel? _form;

    /// <summary>The name for a new target.</summary>
    [ObservableProperty]
    private string _newName = string.Empty;

    /// <summary>Why the new target was not added; null otherwise.</summary>
    [ObservableProperty]
    private string? _notice;

    /// <summary>Selects a target and, after a failure, shows its candidates' match counts.</summary>
    /// <param name="name">The target's name.</param>
    /// <param name="counts">The failure's candidates and counts, or null.</param>
    /// <returns>False when the file has no such target.</returns>
    public bool Select(string name, IReadOnlyList<CandidateMatches>? counts = null)
    {
        if (!Names.Contains(name))
        {
            return false;
        }

        SelectedName = name;
        _counts = counts;
        BuildForm();
        return true;
    }

    /// <summary>Reads the text again; called on every change of the document.</summary>
    public void Refresh()
    {
        Outline = TargetsOutline.Read(_document.Text);
        var keep = SelectedName;
        Names.Clear();
        foreach (var target in Outline.Targets)
        {
            Names.Add(target.Name);
        }

        SelectedName = keep is not null && Names.Contains(keep) ? keep : null;
        BuildForm();
        OnPropertyChanged(nameof(IsAvailable));
        OnPropertyChanged(nameof(Problem));
        OnPropertyChanged(nameof(EmptyText));
    }

    /// <summary>Adds a target with the name typed and one empty test ID candidate, and selects it.</summary>
    [RelayCommand]
    private void AddTarget()
    {
        var name = NewName.Trim();
        Notice = !TargetName().IsMatch(name) ? "A target name starts with a letter and has only letters, digits, dots, dashes and underscores."
            : Names.Contains(name) || _sharedTargets().Contains(name) ? $"There is already a target named {name}."
            : null;
        if (Notice is not null || !IsAvailable)
        {
            return;
        }

        Apply(TargetWriter.Add(Outline, TargetWriter.Write(name, null, null, [new CandidateValue("testId", string.Empty, null, null, null)])), name);
        NewName = string.Empty;
    }

    /// <summary>Removes the selected target.</summary>
    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void RemoveTarget()
    {
        if (Outline.Targets.FirstOrDefault(t => t.Name == SelectedName) is { } target)
        {
            Apply(TargetWriter.Remove(Outline, target), null);
        }
    }

    partial void OnSelectedNameChanged(string? value)
    {
        if (!_editing)
        {
            _counts = null;
            BuildForm();
        }
    }

    private bool HasSelection() => SelectedName is not null;

    private void BuildForm()
    {
        if (Outline.Targets.FirstOrDefault(t => t.Name == SelectedName) is not { } target)
        {
            Form = null;
            return;
        }

        var outline = Outline;
        var others = Names.Concat(_sharedTargets()).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        Form = new TargetFormViewModel(outline, target, others, text =>
        {
            if (ReferenceEquals(outline, Outline))
            {
                Apply(TargetWriter.Replace(outline, target, text), target.Name);
            }
        });
        if (_counts is { } counts)
        {
            Form.ShowCounts(counts);
        }
    }

    private void Apply(string text, string? select)
    {
        _editing = true;
        try
        {
            DocumentText.Replace(_document, text);
            SelectedName = select;
        }
        finally
        {
            _editing = false;
        }

        Refresh();
    }

    [GeneratedRegex(@"^[A-Za-z][A-Za-z0-9_.-]*$", RegexOptions.CultureInvariant)]
    private static partial Regex TargetName();
}
