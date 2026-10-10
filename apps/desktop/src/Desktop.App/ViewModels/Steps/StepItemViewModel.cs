// One step in the step list: its action, its main value, its name, and
// whether the engine reported a problem on its lines.

using CommunityToolkit.Mvvm.ComponentModel;
using Desktop.App.StepFiles;

namespace Desktop.App.ViewModels.Steps;

/// <summary>A step in the step list.</summary>
public sealed partial class StepItemViewModel : ObservableObject
{
    private const int MaxDetail = 60;

    /// <summary>Creates the item.</summary>
    /// <param name="outline">The step as read from the file.</param>
    /// <param name="text">The file's text, for values.</param>
    /// <param name="shorthand">The action's shorthand parameter, if known.</param>
    public StepItemViewModel(StepOutlineItem outline, string text, string? shorthand)
    {
        ArgumentNullException.ThrowIfNull(outline);
        ArgumentNullException.ThrowIfNull(text);
        Outline = outline;
        Detail = Shorten(DetailOf(outline, text, shorthand));
        Name = outline.Settings.FirstOrDefault(s => s.Key.Value == "name").Value is YamlScalar name ? name.Value : null;
    }

    /// <summary>The step as read from the file.</summary>
    public StepOutlineItem Outline { get; }

    /// <summary>The section's key.</summary>
    public string Section => Outline.Section;

    /// <summary>The action, or a note that the step cannot be read.</summary>
    public string Action => Outline.Form == StepForm.Unreadable ? "(not a step the list can read)" : Outline.Action;

    /// <summary>The main value: the shorthand value, or the parameters in short.</summary>
    public string Detail { get; }

    /// <summary>The step's own name, if it has one.</summary>
    public string? Name { get; }

    /// <summary>True when it has a name.</summary>
    public bool HasName => Name is not null;

    /// <summary>"line 12" or "lines 12–15".</summary>
    public string LinesText => Outline.Line == Outline.EndLine ? $"line {Outline.Line}" : $"lines {Outline.Line}–{Outline.EndLine}";

    /// <summary>Errors on its lines.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasWarningOnly))]
    private bool _hasError;

    /// <summary>Warnings on its lines.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasWarningOnly))]
    private bool _hasWarning;

    /// <summary>True when it has warnings and no errors.</summary>
    public bool HasWarningOnly => HasWarning && !HasError;

    /// <summary>True when it is the selected step.</summary>
    [ObservableProperty]
    private bool _isSelected;

    /// <summary>Marks the step from the file's problem lines.</summary>
    /// <param name="marks">Problem marks by line.</param>
    public void ApplyMarks(IReadOnlyDictionary<int, LineMark> marks)
    {
        ArgumentNullException.ThrowIfNull(marks);
        var mine = Enumerable.Range(Outline.Line, Outline.EndLine - Outline.Line + 1).Select(l => marks.GetValueOrDefault(l)).ToList();
        HasError = mine.Contains(LineMark.Error);
        HasWarning = mine.Contains(LineMark.Warning);
    }

    private static string DetailOf(StepOutlineItem step, string text, string? shorthand)
    {
        switch (step.Value)
        {
            case YamlScalar scalar:
                return scalar.Value;
            case YamlMapping mapping when shorthand is not null && mapping.Get(shorthand) is YamlScalar main:
                return main.Value;
            case YamlMapping mapping:
                return string.Join(", ", mapping.Entries.Select(e => e.Value is YamlScalar v ? $"{e.Key.Value}: {v.Value}" : e.Key.Value));
            case YamlNode other:
                return OneLine(text[other.Start..other.End]);
            default:
                return step.Form == StepForm.Unreadable ? OneLine(text[step.Node.Start..step.Node.End]) : string.Empty;
        }
    }

    private static string OneLine(string text) => string.Join(' ', text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).Select(l => l.Trim()));

    private static string Shorten(string text) => text.Length <= MaxDetail ? text : text[..(MaxDetail - 1)] + "…";
}
