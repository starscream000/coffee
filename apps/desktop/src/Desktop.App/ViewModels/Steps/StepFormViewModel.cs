// The form of the selected step: a field per parameter of its action (from the
// action's parameter schema), its name, page and timeout, and the engine's
// problems on its lines, next to the field they are about where possible. A
// valid change rewrites the step's own lines at once.

using System.Collections.ObjectModel;
using Desktop.App.StepFiles;
using Desktop.Protocol.Messages;

namespace Desktop.App.ViewModels.Steps;

/// <summary>The form of one step.</summary>
public sealed class StepFormViewModel
{
    private static readonly IReadOnlyList<string> ShownSettings = ["name", "page", "timeout"];

    /// <summary>The field name of a shorthand value of an action the engine does not know.</summary>
    private const string UnnamedValue = "(value)";

    private readonly StepOutlineItem _step;
    private readonly StepActionChoice? _action;
    private readonly IReadOnlyList<KeyValuePair<YamlScalar, YamlNode>> _otherSettings;
    private readonly string _text;
    private readonly Action<string> _apply;

    /// <summary>Creates the form.</summary>
    /// <param name="outline">The file.</param>
    /// <param name="step">The step.</param>
    /// <param name="action">What the engine says about the step's action; null when it does not know it.</param>
    /// <param name="targetNames">The targets the picker offers.</param>
    /// <param name="diagnostics">The file's problems.</param>
    /// <param name="apply">Puts the step's new text (without indentation) in place of its lines.</param>
    public StepFormViewModel(StepOutline outline, StepOutlineItem step, StepActionChoice? action, IReadOnlyList<string> targetNames, IReadOnlyList<Diagnostic> diagnostics, Action<string> apply)
    {
        ArgumentNullException.ThrowIfNull(outline);
        ArgumentNullException.ThrowIfNull(step);
        ArgumentNullException.ThrowIfNull(diagnostics);
        _step = step;
        _action = action;
        _text = outline.Text;
        _apply = apply;
        _otherSettings = [.. step.Settings.Where(s => !ShownSettings.Contains(s.Key.Value))];
        Action = step.Action;
        Description = action?.Description;
        if (step.Form == StepForm.Unreadable)
        {
            Notice = "This step cannot be shown as a form: it needs exactly one action. Edit it as text.";
        }
        else if (action is null)
        {
            Notice = $"The engine does not know the action \"{step.Action}\", so its parameters are shown as YAML.";
        }

        if (step.Form != StepForm.Unreadable)
        {
            BuildParameters(targetNames);
            foreach (var name in ShownSettings)
            {
                var setting = step.Settings.FirstOrDefault(s => s.Key.Value == name);
                Settings.Add(Field(new ParamField(name, FieldKind.Text, false, [], null), setting.Value, setting.Key is { } key ? (key.Start, setting.Value.End) : None, true));
            }
        }

        // A problem goes to the field whose key or value it points at (the step format says
        // which); the others, such as a missing parameter, are listed for the step.
        var lineStarts = LineStarts(outline.Text);
        var inStep = diagnostics.Where(d => d.Line >= step.Line && d.Line <= step.EndLine).ToList();
        foreach (var field in Parameters.Concat(Settings))
        {
            var mine = inStep.Where(d => Offset(lineStarts, d) is var at && at >= field.Span.Start && at < Math.Max(field.Span.End, field.Span.Start + 1)).ToList();
            field.Problem = mine.Count == 0 ? null : string.Join(Environment.NewLine, mine.Select(Describe));
            inStep.RemoveAll(mine.Contains);
        }

        foreach (var d in inStep)
        {
            Problems.Add(Describe(d));
        }
    }

    /// <summary>The action's name.</summary>
    public string Action { get; }

    /// <summary>What the action does.</summary>
    public string? Description { get; }

    /// <summary>Why the form is limited, if it is.</summary>
    public string? Notice { get; }

    /// <summary>The action's parameters.</summary>
    public ObservableCollection<StepFieldViewModel> Parameters { get; } = [];

    /// <summary>The step's name, page and timeout.</summary>
    public ObservableCollection<StepFieldViewModel> Settings { get; } = [];

    /// <summary>The engine's problems on the step that are not about one field.</summary>
    public ObservableCollection<string> Problems { get; } = [];

    /// <summary>True when there are problems not shown at a field.</summary>
    public bool HasProblems => Problems.Count > 0;

    /// <summary>The step's text as the form's fields give it.</summary>
    /// <returns>The text without indentation.</returns>
    /// <exception cref="FormatException">A field's value does not fit it.</exception>
    public string Write()
    {
        var parameters = Parameters.Select(f => new StepValue(f.Name, f.ToYaml())).ToList();
        var settings = _step.Settings.Select(s => s.Key.Value)
            .Concat(ShownSettings.Where(n => _step.Settings.All(s => s.Key.Value != n)))
            .Select(name => Settings.FirstOrDefault(f => f.Name == name) is { } field
                ? new StepValue(name, field.ToYaml())
                : new StepValue(name, StepWriter.Raw(_text, _otherSettings.First(s => s.Key.Value == name).Value)))
            .ToList();
        var shorthand = _action?.Shorthand ?? (Parameters.Count == 1 && Parameters[0].Name == UnnamedValue ? UnnamedValue : null);
        return StepWriter.Write(_step.Action, _step, shorthand, parameters, settings);
    }

    private static (int, int) None => (-1, -1);

    private static List<int> LineStarts(string text)
    {
        var starts = new List<int> { 0 };
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\n')
            {
                starts.Add(i + 1);
            }
        }

        return starts;
    }

    private static int Offset(List<int> lineStarts, Diagnostic d) =>
        d.Line >= 1 && d.Line <= lineStarts.Count ? lineStarts[d.Line - 1] + Math.Max(d.Column - 1, 0) : -1;

    private static string Describe(Diagnostic d) => d.Hint is { } hint ? $"{d.Message} {hint}" : d.Message;

    private void BuildParameters(IReadOnlyList<string> targetNames)
    {
        var known = _action is null ? [] : ParamSchema.Read(_action.Schema);
        var written = _step.Value switch
        {
            YamlMapping mapping => mapping.Entries.Select(e => (e.Key.Value, (YamlNode?)e.Value, (e.Key.Start, e.Value.End))).ToList(),
            YamlNode shorthand when _action?.Shorthand is { } name => [(name, shorthand, (shorthand.Start, shorthand.End))],
            YamlNode other => [(UnnamedValue, other, (other.Start, other.End))],
            _ => [],
        };

        // The parameters as written first, in their order, then the others the action has.
        foreach (var (name, node, span) in written)
        {
            var field = known.FirstOrDefault(f => f.Name == name);
            Parameters.Add(Field(field ?? new ParamField(name, FieldKind.Yaml, false, [], null), node, span, field is not null, targetNames));
        }

        foreach (var field in known.Where(f => written.All(w => w.Item1 != f.Name)))
        {
            Parameters.Add(Field(field, null, None, true, targetNames));
        }
    }

    private StepFieldViewModel Field(ParamField field, YamlNode? node, (int Start, int End) span, bool isKnown, IReadOnlyList<string>? targetNames = null) =>
        new(field, node, _text, span, isKnown, _ => Changed()) { TargetNames = targetNames ?? [] };

    private void Changed()
    {
        string text;
        try
        {
            text = Write();
        }
        catch (FormatException)
        {
            // The field with the bad value shows why; nothing is written until it is fixed.
            return;
        }

        _apply(text);
    }
}
