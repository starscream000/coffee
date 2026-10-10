// The step list beside a test or flow file's text: the file's sections with
// their steps, kept in step with the text. Every change made here is a change
// of the text (one replacement of only the lines concerned), so the text
// editor shows it at once and its undo covers it; every change of the text is
// read back here at once.

using System.Collections.ObjectModel;
using AvaloniaEdit.Document;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Desktop.App.StepFiles;

namespace Desktop.App.ViewModels.Steps;

/// <summary>A section in the step list.</summary>
/// <param name="Name">Its key: <c>before</c>, <c>steps</c> or <c>after</c>.</param>
public sealed record StepSectionViewModel(string Name)
{
    /// <summary>Its steps.</summary>
    public ObservableCollection<StepItemViewModel> Steps { get; } = [];

    /// <summary>True when it has no steps.</summary>
    public bool IsEmpty => Steps.Count == 0;
}

/// <summary>An action that can be added as a step.</summary>
/// <param name="Name">The action's name.</param>
/// <param name="Description">What it does.</param>
/// <param name="Shorthand">Its shorthand parameter, if it has one.</param>
/// <param name="Required">The parameters its long form requires.</param>
public sealed record StepActionChoice(string Name, string Description, string? Shorthand, IReadOnlyList<string> Required);

/// <summary>The step list of a test or flow file.</summary>
public sealed partial class StepListViewModel : ObservableObject
{
    private readonly string _file;
    private readonly TextDocument _document;
    private readonly Func<IReadOnlyList<StepActionChoice>> _actions;
    private readonly Func<IReadOnlyDictionary<int, LineMark>> _marks;
    private bool _editing;

    /// <summary>Creates the list and reads the document.</summary>
    /// <param name="file">The file, for its kind.</param>
    /// <param name="document">The text, shared with the text editor.</param>
    /// <param name="actions">The actions the engine knows, for adding steps and for shorthand names.</param>
    /// <param name="marks">The file's problem lines.</param>
    public StepListViewModel(string file, TextDocument document, Func<IReadOnlyList<StepActionChoice>> actions, Func<IReadOnlyDictionary<int, LineMark>> marks)
    {
        ArgumentNullException.ThrowIfNull(document);
        _file = file;
        _document = document;
        _actions = actions;
        _marks = marks;
        Outline = StepOutline.Read(file, string.Empty);
        _document.TextChanged += (_, _) =>
        {
            if (!_editing)
            {
                Refresh();
            }
        };
        Refresh();
    }

    /// <summary>Raised when a step is selected, with its first line, so the text editor can show it.</summary>
    public event EventHandler<int>? StepSelected;

    /// <summary>The file as last read.</summary>
    public StepOutline Outline { get; private set; }

    /// <summary>The sections, in order.</summary>
    public ObservableCollection<StepSectionViewModel> Sections { get; } = [];

    /// <summary>The sections the selected step can move to: all but its own.</summary>
    public IReadOnlyList<string> MoveTargets => Selected is { } step ? [.. Sections.Select(s => s.Name).Where(n => n != step.Section)] : [];

    /// <summary>True when the file can be shown as steps.</summary>
    public bool IsAvailable => Outline.IsReadable;

    /// <summary>Why the file is shown as text only; null when the step list can show it.</summary>
    public string? Problem => Outline.IsReadable ? null : $"The step list cannot show this file. {Outline.Problem} Edit it as text.";

    /// <summary>The selected step.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RemoveCommand), nameof(DuplicateCommand), nameof(MoveUpCommand), nameof(MoveDownCommand), nameof(MoveToSectionCommand))]
    private StepItemViewModel? _selected;

    /// <summary>True while the action picker is open.</summary>
    [ObservableProperty]
    private bool _isPicking;

    /// <summary>Filters the action picker by name and description.</summary>
    [ObservableProperty]
    private string _pickerSearch = string.Empty;

    /// <summary>The actions the picker offers, after the search.</summary>
    public ObservableCollection<StepActionChoice> PickerItems { get; } = [];

    /// <summary>Selects a step and shows it in the text editor.</summary>
    /// <param name="step">The step.</param>
    [RelayCommand]
    public void Select(StepItemViewModel? step)
    {
        Selected = step;
        if (step is not null)
        {
            StepSelected?.Invoke(this, step.Outline.Line);
        }
    }

    /// <summary>Reads the text again; called on every change of the document.</summary>
    public void Refresh()
    {
        var keep = Selected is { } selected ? (selected.Section, selected.Outline.Index) : ((string, int)?)null;
        Outline = StepOutline.Read(_file, _document.Text);
        var shorthands = _actions().ToDictionary(a => a.Name, a => a.Shorthand, StringComparer.Ordinal);
        var marks = _marks();
        Sections.Clear();
        foreach (var section in Outline.Sections)
        {
            var view = new StepSectionViewModel(section.Name);
            foreach (var step in section.Steps)
            {
                var item = new StepItemViewModel(step, Outline.Text, shorthands.GetValueOrDefault(step.Action));
                item.ApplyMarks(marks);
                view.Steps.Add(item);
            }

            Sections.Add(view);
        }

        Selected = keep is { } k ? Find(k.Item1, k.Item2) : null;
        if (Selected is { } now)
        {
            now.IsSelected = true;
        }

        OnPropertyChanged(nameof(IsAvailable));
        OnPropertyChanged(nameof(Problem));
    }

    /// <summary>Marks the steps again after the file's problems changed.</summary>
    public void RefreshMarks()
    {
        var marks = _marks();
        foreach (var step in Sections.SelectMany(s => s.Steps))
        {
            step.ApplyMarks(marks);
        }
    }

    /// <summary>Opens the action picker.</summary>
    [RelayCommand]
    private void StartAdding()
    {
        PickerSearch = string.Empty;
        FilterPicker();
        IsPicking = true;
    }

    /// <summary>Closes the action picker without adding.</summary>
    [RelayCommand]
    private void CancelAdding() => IsPicking = false;

    /// <summary>Adds a step for an action after the selected step, or at the end of the steps when none is selected.</summary>
    /// <param name="action">The action.</param>
    [RelayCommand]
    public void Add(StepActionChoice? action)
    {
        if (action is null || !IsAvailable)
        {
            return;
        }

        IsPicking = false;
        var section = Selected?.Section ?? "steps";
        var after = Selected?.Outline.Index;
        var text = StepEdits.Insert(Outline, section, after, NewStep.Text(action.Name, action.Shorthand, action.Required));
        Apply(text, section, after is { } i ? i + 1 : Outline.Sections.Single(s => s.Name == section).Steps.Count);
    }

    /// <summary>Removes the selected step.</summary>
    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void Remove()
    {
        if (Selected is { } step)
        {
            var count = Outline.Sections.Single(s => s.Name == step.Section).Steps.Count;
            Apply(StepEdits.Remove(Outline, step.Outline), step.Section, Math.Min(step.Outline.Index, count - 2));
        }
    }

    /// <summary>Inserts a copy of the selected step after it.</summary>
    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void Duplicate()
    {
        if (Selected is { } step)
        {
            Apply(StepEdits.Duplicate(Outline, step.Outline), step.Section, step.Outline.Index + 1);
        }
    }

    /// <summary>Moves the selected step up in its section.</summary>
    [RelayCommand(CanExecute = nameof(CanMoveUp))]
    private void MoveUp()
    {
        if (Selected is { } step)
        {
            Apply(StepEdits.Move(Outline, step.Outline, -1), step.Section, step.Outline.Index - 1);
        }
    }

    /// <summary>Moves the selected step down in its section.</summary>
    [RelayCommand(CanExecute = nameof(CanMoveDown))]
    private void MoveDown()
    {
        if (Selected is { } step)
        {
            Apply(StepEdits.Move(Outline, step.Outline, +1), step.Section, step.Outline.Index + 1);
        }
    }

    /// <summary>Moves the selected step to the end of another section.</summary>
    /// <param name="section">The section's key.</param>
    [RelayCommand(CanExecute = nameof(CanMoveToSection))]
    private void MoveToSection(string? section)
    {
        if (Selected is not { } step || section is null || section == step.Section)
        {
            return;
        }

        var text = StepEdits.MoveToSection(Outline, step.Outline, section);
        var target = Outline.Sections.Single(s => s.Name == section).Steps.Count;
        Apply(text, section, target);
    }

    partial void OnPickerSearchChanged(string value) => FilterPicker();

    partial void OnSelectedChanged(StepItemViewModel? oldValue, StepItemViewModel? newValue)
    {
        if (oldValue is not null)
        {
            oldValue.IsSelected = false;
        }

        if (newValue is not null)
        {
            newValue.IsSelected = true;
        }

        OnPropertyChanged(nameof(MoveTargets));
    }

    private bool HasSelection() => Selected is not null;

    private bool CanMoveUp() => Selected is { Outline.Index: > 0 };

    private bool CanMoveDown() => Selected is { } s && s.Outline.Index < Outline.Sections.Single(x => x.Name == s.Section).Steps.Count - 1;

    private bool CanMoveToSection(string? section) => Selected is not null && Sections.Count > 1;

    private StepItemViewModel? Find(string section, int index) =>
        Sections.FirstOrDefault(s => s.Name == section) is { } found && index >= 0 && index < found.Steps.Count ? found.Steps[index] : null;

    private void FilterPicker()
    {
        var search = PickerSearch.Trim();
        PickerItems.Clear();
        foreach (var action in _actions().Where(a => search.Length == 0
            || a.Name.Contains(search, StringComparison.OrdinalIgnoreCase)
            || a.Description.Contains(search, StringComparison.OrdinalIgnoreCase)))
        {
            PickerItems.Add(action);
        }
    }

    /// <summary>
    /// Puts a new text into the document as one replacement of the part that
    /// differs, so the text editor's undo takes it back in one step, then
    /// selects the step at a position.
    /// </summary>
    private void Apply(string text, string section, int index)
    {
        var old = _document.Text;
        if (text == old)
        {
            return;
        }

        var prefix = 0;
        var max = Math.Min(old.Length, text.Length);
        while (prefix < max && old[prefix] == text[prefix])
        {
            prefix++;
        }

        var suffix = 0;
        while (suffix < max - prefix && old[old.Length - 1 - suffix] == text[text.Length - 1 - suffix])
        {
            suffix++;
        }

        _editing = true;
        try
        {
            _document.Replace(prefix, old.Length - prefix - suffix, text.Substring(prefix, text.Length - prefix - suffix));
        }
        finally
        {
            _editing = false;
        }

        Selected = null;
        Refresh();
        Select(Find(section, index));
    }
}
