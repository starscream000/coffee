// One candidate of a target in the targets editor: its kind and value, the
// accessible name of a role candidate, exact and nth. A candidate the editor
// cannot show field by field (another key, a value that is not a scalar) is
// kept as written. After a failure, it shows how many elements it matched.

using System.Text.Json;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using Desktop.App.StepFiles;

namespace Desktop.App.ViewModels.Targets;

/// <summary>A candidate in the targets editor.</summary>
public sealed partial class CandidateViewModel : ObservableObject
{
    private static readonly HashSet<string> Known = new(TargetsOutline.CandidateKinds.Append("name").Append("exact").Append("nth"), StringComparer.Ordinal);

    private readonly string? _raw;
    private readonly Action _changed;
    private readonly (string Kind, string Value, string Name, string Exact, string Nth) _initial;
    private readonly bool _loading = true;

    /// <summary>Creates the candidate.</summary>
    /// <param name="node">The candidate in the file; null for a new one.</param>
    /// <param name="raw">Its original lines (<see cref="TargetWriter.CandidateTexts"/>); null for a new one.</param>
    /// <param name="changed">Called when a valid change was made.</param>
    public CandidateViewModel(YamlNode? node, string? raw, Action changed)
    {
        _changed = changed;
        _raw = raw;
        if (node is YamlMapping mapping
            && mapping.Entries.All(e => Known.Contains(e.Key.Value) && e.Value is YamlScalar)
            && mapping.Entries.Count(e => TargetsOutline.CandidateKinds.Contains(e.Key.Value)) == 1)
        {
            string Get(string key) => mapping.Get(key) is YamlScalar s ? s.Value : string.Empty;
            var kind = mapping.Entries.First(e => TargetsOutline.CandidateKinds.Contains(e.Key.Value)).Key.Value;
            _initial = (kind, Get(kind), Get("name"), Get("exact"), Get("nth"));
        }
        else if (node is not null)
        {
            IsKeptAsWritten = true;
            _initial = (string.Empty, string.Empty, string.Empty, string.Empty, string.Empty);
        }
        else
        {
            _initial = ("testId", string.Empty, string.Empty, string.Empty, string.Empty);
        }

        (Kind, Value, Name, Exact, Nth) = _initial;
        _loading = false;
    }

    /// <summary>The kinds offered, in order of reliability.</summary>
    public static IReadOnlyList<string> Kinds => TargetsOutline.CandidateKinds;

    /// <summary>The choices for <c>exact</c>: not set, true, false.</summary>
    public IReadOnlyList<string> ExactChoices { get; } = [string.Empty, "true", "false"];

    /// <summary>True for a candidate the editor keeps as written.</summary>
    public bool IsKeptAsWritten { get; }

    /// <summary>True for a candidate edited field by field.</summary>
    public bool IsEditable => !IsKeptAsWritten;

    /// <summary>The candidate's text, for one kept as written.</summary>
    public string RawText => _raw ?? string.Empty;

    /// <summary>The kind.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRole))]
    private string _kind = "testId";

    /// <summary>The kind's value.</summary>
    [ObservableProperty]
    private string _value = string.Empty;

    /// <summary>The accessible name, for a role candidate.</summary>
    [ObservableProperty]
    private string _name = string.Empty;

    /// <summary><c>exact</c>: empty, <c>true</c> or <c>false</c>.</summary>
    [ObservableProperty]
    private string _exact = string.Empty;

    /// <summary><c>nth</c>, or empty.</summary>
    [ObservableProperty]
    private string _nth = string.Empty;

    /// <summary>Why the candidate cannot be written as it is; null when it can.</summary>
    [ObservableProperty]
    private string? _error;

    /// <summary>"matched 2 elements" after a failure that counted this candidate; null otherwise.</summary>
    [ObservableProperty]
    private string? _matchText;

    /// <summary>True for a role candidate, which may have a name.</summary>
    public bool IsRole => Kind == "role";

    /// <summary>The candidate as the writer takes it.</summary>
    /// <returns>Its value; the original text when nothing changed.</returns>
    /// <exception cref="FormatException">A field does not fit (also set in <see cref="Error"/>).</exception>
    public CandidateValue ToValue()
    {
        if (IsKeptAsWritten || (Kind, Value, Name, Exact, Nth) == _initial && _raw is not null)
        {
            return new CandidateValue(Kind, Value, null, null, null, _raw);
        }

        if (_raw is null && (Kind, Value, Name, Exact, Nth) == _initial)
        {
            // Just added: written empty, so the engine points at it until a value is entered.
            return new CandidateValue(Kind, string.Empty, null, null, null);
        }

        if (Value.Trim().Length == 0 && Kind != "role")
        {
            throw new FormatException($"Enter the {Kind} to look for.");
        }

        var nth = Nth.Trim();
        if (nth.Length > 0 && !NonNegative().IsMatch(nth) && !Interpolation().IsMatch(nth))
        {
            throw new FormatException("nth is a number from 0, or ${…}.");
        }

        return new CandidateValue(
            Kind,
            Value,
            IsRole && Name.Length > 0 ? Name : null,
            Exact switch { "true" => true, "false" => false, _ => null },
            nth.Length == 0 ? null : (NonNegative().IsMatch(nth) ? nth : StepWriter.Text(nth)));
    }

    /// <summary>True when this is the candidate a failure counted.</summary>
    /// <param name="candidate">The candidate as the engine reported it.</param>
    /// <returns>True when kind, value and settings are the same.</returns>
    public bool Matches(IReadOnlyDictionary<string, JsonElement> candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        if (IsKeptAsWritten)
        {
            return false;
        }

        string Text(JsonElement e) => e.ValueKind == JsonValueKind.String ? e.GetString()! : e.GetRawText();
        var reported = candidate.ToDictionary(p => p.Key, p => Text(p.Value), StringComparer.Ordinal);
        var mine = new Dictionary<string, string>(StringComparer.Ordinal) { [Kind] = Value };
        if (IsRole && Name.Length > 0)
        {
            mine["name"] = Name;
        }

        if (Exact.Length > 0)
        {
            mine["exact"] = Exact;
        }

        if (Nth.Length > 0)
        {
            mine["nth"] = Nth;
        }

        return reported.Count == mine.Count && reported.All(p => mine.TryGetValue(p.Key, out var v) && v == p.Value);
    }

    partial void OnKindChanged(string value) => Changed();

    partial void OnValueChanged(string value) => Changed();

    partial void OnNameChanged(string value) => Changed();

    partial void OnExactChanged(string value) => Changed();

    partial void OnNthChanged(string value) => Changed();

    private void Changed()
    {
        if (_loading)
        {
            return;
        }

        try
        {
            ToValue();
            Error = null;
            _changed();
        }
        catch (FormatException ex)
        {
            Error = ex.Message;
        }
    }

    [GeneratedRegex(@"^\d+$", RegexOptions.CultureInvariant)]
    private static partial Regex NonNegative();

    [GeneratedRegex(@"^\$\{[^{}]+\}$", RegexOptions.CultureInvariant)]
    private static partial Regex Interpolation();
}
