// One field of a step's form: a parameter of the action or a setting of the
// step (name, page, timeout). It shows the value as written in the file and
// turns what is typed back into YAML, refusing what does not fit its kind.

using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using Desktop.App.StepFiles;

namespace Desktop.App.ViewModels.Steps;

/// <summary>A field of a step's form.</summary>
public sealed partial class StepFieldViewModel : ObservableObject
{
    private readonly string? _originalYaml;
    private readonly string _initial;
    private readonly Action<StepFieldViewModel> _changed;

    /// <summary>Creates the field.</summary>
    /// <param name="field">What the action's schema says about it.</param>
    /// <param name="original">Its value in the file, if it has one.</param>
    /// <param name="text">The file's text, for the value's original YAML.</param>
    /// <param name="span">Where it is in the text, from its key to the end of its value; (−1, −1) when the step does not have it.</param>
    /// <param name="isKnown">False for a key the action's schema does not list.</param>
    /// <param name="changed">Called when a valid new value was entered.</param>
    public StepFieldViewModel(ParamField field, YamlNode? original, string text, (int Start, int End) span, bool isKnown, Action<StepFieldViewModel> changed)
    {
        ArgumentNullException.ThrowIfNull(field);
        ArgumentNullException.ThrowIfNull(text);
        Name = field.Name;
        IsRequired = field.IsRequired;
        IsKnown = isKnown;
        Span = span;
        _changed = changed;
        Kind = field.Kind == FieldKind.Target && original is not (null or YamlScalar) ? FieldKind.Yaml : field.Kind;
        IsInlineTarget = field.Kind == FieldKind.Target && Kind == FieldKind.Yaml;
        Choices = Kind == FieldKind.Choice && !IsRequired ? [string.Empty, .. field.Choices] : field.Choices;
        Description = field.Description;
        _originalYaml = original is null ? null : StepWriter.Raw(text, original);
        _initial = original switch
        {
            null => string.Empty,
            YamlScalar scalar when Kind != FieldKind.Yaml => scalar.Value,
            _ => _originalYaml!,
        };
        _value = _initial;
    }

    /// <summary>The key.</summary>
    public string Name { get; }

    /// <summary>"target *" for a required parameter, else the key.</summary>
    public string Label => IsRequired ? $"{Name} *" : Name;

    /// <summary>How it is edited.</summary>
    public FieldKind Kind { get; }

    /// <summary>True when the action's long form must give it.</summary>
    public bool IsRequired { get; }

    /// <summary>False for a key the action's schema does not list; it is kept as written.</summary>
    public bool IsKnown { get; }

    /// <summary>True for a target written inline in the step, which is edited as YAML.</summary>
    public bool IsInlineTarget { get; }

    /// <summary>Where it is in the text, from its key to the end of its value; (−1, −1) when the step does not have it.</summary>
    public (int Start, int End) Span { get; }

    /// <summary>The values a choice offers; the first is empty for an optional one.</summary>
    public IReadOnlyList<string> Choices { get; }

    /// <summary>What the schema says about it, or a note about how it is edited.</summary>
    public string? Description { get; }

    /// <summary>A note shown under the field: an inline target, or a key the action does not know.</summary>
    public string? Note => IsInlineTarget
        ? "An inline target, edited as YAML."
        : IsKnown ? null : "Not a parameter of this action; kept as written.";

    /// <summary>True for a one-line text field.</summary>
    public bool IsLine => Kind is FieldKind.Text or FieldKind.Number;

    /// <summary>True for a list of choices.</summary>
    public bool IsChoice => Kind is FieldKind.Choice or FieldKind.Boolean;

    /// <summary>True for the target picker.</summary>
    public bool IsTarget => Kind == FieldKind.Target;

    /// <summary>True for YAML text.</summary>
    public bool IsYaml => Kind == FieldKind.Yaml;

    /// <summary>The target names the picker offers.</summary>
    public IReadOnlyList<string> TargetNames { get; init; } = [];

    /// <summary>The value as typed.</summary>
    [ObservableProperty]
    private string _value;

    /// <summary>Why the typed value cannot be written; null when it can.</summary>
    [ObservableProperty]
    private string? _error;

    /// <summary>The engine's problems on this field's line; null when none.</summary>
    [ObservableProperty]
    private string? _problem;

    /// <summary>True when the value differs from the file's.</summary>
    public bool IsChanged => Value != _initial;

    /// <summary>The value as YAML for the step: the original text when unchanged, null to leave the key out.</summary>
    /// <returns>The YAML, or null.</returns>
    /// <exception cref="FormatException">The typed value does not fit the field (also set in <see cref="Error"/>).</exception>
    public string? ToYaml()
    {
        if (!IsChanged)
        {
            return _originalYaml;
        }

        var value = Value.Replace("\r\n", "\n", StringComparison.Ordinal);
        if (value.Trim().Length == 0)
        {
            return IsRequired && Kind != FieldKind.Yaml ? "''" : null;
        }

        return Kind switch
        {
            FieldKind.Number => StepWriter.Number(value) ?? throw new FormatException("Enter a number, or ${…}."),
            FieldKind.Boolean => value.Trim() is "true" or "false" ? value.Trim()
                : Interpolation().IsMatch(value.Trim()) ? StepWriter.Text(value.Trim())
                : throw new FormatException("Choose true or false, or enter ${…}."),
            FieldKind.Target => TargetName().IsMatch(value.Trim()) || Interpolation().IsMatch(value.Trim())
                ? StepWriter.Text(value.Trim())
                : throw new FormatException("A target name starts with a letter and has only letters, digits, dots, dashes and underscores."),
            FieldKind.Yaml => CheckYaml(value.TrimEnd('\n')),
            _ when Name == "timeout" && !Timeout().IsMatch(value.Trim()) && !Interpolation().IsMatch(value.Trim()) =>
                throw new FormatException("A timeout is a number with ms, s or m, such as 500ms, 10s or 2m."),
            _ => StepWriter.Text(value),
        };
    }

    partial void OnValueChanged(string value)
    {
        try
        {
            ToYaml();
            Error = null;
            _changed(this);
        }
        catch (FormatException ex)
        {
            Error = ex.Message;
        }
    }

    private static string CheckYaml(string yaml)
    {
        try
        {
            YamlTree.Read(yaml);
            return yaml;
        }
        catch (YamlTreeException ex)
        {
            throw new FormatException(ex.Message, ex);
        }
    }

    [GeneratedRegex(@"^\$\{[^{}]+\}$", RegexOptions.CultureInvariant)]
    private static partial Regex Interpolation();

    [GeneratedRegex(@"^[A-Za-z][A-Za-z0-9_.-]*$", RegexOptions.CultureInvariant)]
    private static partial Regex TargetName();

    [GeneratedRegex(@"^\d+(ms|s|m)$", RegexOptions.CultureInvariant)]
    private static partial Regex Timeout();
}
