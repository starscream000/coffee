// The action catalogue: every built-in and user action the engine reports, with
// its description, short form and parameters.

using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using Desktop.Protocol.Messages;

namespace Desktop.App.ViewModels;

/// <summary>One parameter of an action, from its parameter schema.</summary>
/// <param name="Name">The parameter's name.</param>
/// <param name="Type">Its JSON type, or a short description of a union.</param>
/// <param name="IsRequired">True when the long form must give it.</param>
/// <param name="Description">Its description, if the schema has one.</param>
public sealed record ActionParameterViewModel(string Name, string Type, bool IsRequired, string? Description);

/// <summary>One action in the catalogue.</summary>
public sealed class ActionItemViewModel
{
    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    /// <summary>Creates the item.</summary>
    /// <param name="action">What the engine reported.</param>
    public ActionItemViewModel(ActionInfo action)
    {
        ArgumentNullException.ThrowIfNull(action);
        Action = action;
        Parameters = ReadParameters(action.ParamsSchema);
        SchemaText = JsonSerializer.Serialize(action.ParamsSchema, Indented);
    }

    /// <summary>What the engine reported.</summary>
    public ActionInfo Action { get; }

    /// <summary>The action's name.</summary>
    public string Name => Action.Name;

    /// <summary>What it does.</summary>
    public string Description => Action.Description;

    /// <summary>True for an action written in the project.</summary>
    public bool IsUserAction => Action.Source.Kind == ActionSourceKind.File;

    /// <summary>"built-in", or the file of a user action.</summary>
    public string SourceText => IsUserAction ? Action.Source.File ?? "user action" : "built-in";

    /// <summary>How the short form is written, or that there is none.</summary>
    public string ShorthandText => Action.Shorthand is { } p
        ? $"Short form: \"- {Name}: <{p}>\" fills \"{p}\"."
        : "No short form: write the parameters as a map.";

    /// <summary>The parameters of the long form.</summary>
    public IReadOnlyList<ActionParameterViewModel> Parameters { get; }

    /// <summary>True when the schema lists parameters.</summary>
    public bool HasParameters => Parameters.Count > 0;

    /// <summary>The parameter schema as indented JSON.</summary>
    public string SchemaText { get; }

    private static List<ActionParameterViewModel> ReadParameters(JsonElement schema)
    {
        if (schema.ValueKind != JsonValueKind.Object || !schema.TryGetProperty("properties", out var properties) || properties.ValueKind != JsonValueKind.Object)
        {
            return [];
        }

        var required = schema.TryGetProperty("required", out var r) && r.ValueKind == JsonValueKind.Array
            ? r.EnumerateArray().Select(e => e.GetString()).ToHashSet(StringComparer.Ordinal)
            : [];
        return [.. properties.EnumerateObject().Select(p => new ActionParameterViewModel(
            p.Name,
            TypeOf(p.Value),
            required.Contains(p.Name),
            p.Value.ValueKind == JsonValueKind.Object && p.Value.TryGetProperty("description", out var d) && d.ValueKind == JsonValueKind.String ? d.GetString() : null))];
    }

    private static string TypeOf(JsonElement property)
    {
        if (property.ValueKind != JsonValueKind.Object)
        {
            return "any";
        }

        if (property.TryGetProperty("type", out var type))
        {
            return type.ValueKind == JsonValueKind.Array
                ? string.Join(" | ", type.EnumerateArray().Select(t => t.GetString()))
                : type.GetString() ?? "any";
        }

        if (property.TryGetProperty("enum", out var values) && values.ValueKind == JsonValueKind.Array)
        {
            return string.Join(" | ", values.EnumerateArray().Select(v => v.ToString()));
        }

        foreach (var union in new[] { "anyOf", "oneOf" })
        {
            if (property.TryGetProperty(union, out var options) && options.ValueKind == JsonValueKind.Array)
            {
                return string.Join(" | ", options.EnumerateArray().Select(TypeOf).Distinct(StringComparer.Ordinal));
            }
        }

        return property.TryGetProperty("$ref", out _) ? "target" : "any";
    }
}

/// <summary>The action catalogue tab.</summary>
public sealed partial class ActionCatalogViewModel : WorkspaceTabViewModel
{
    private IReadOnlyList<ActionItemViewModel> _all = [];

    /// <inheritdoc />
    public override string Title => "Actions";

    /// <inheritdoc />
    public override bool CanClose => false;

    /// <summary>Every action the engine listed, in its order.</summary>
    public IReadOnlyList<ActionItemViewModel> All => _all;

    /// <summary>The actions shown, after the search.</summary>
    public ObservableCollection<ActionItemViewModel> Items { get; } = [];

    /// <summary>Shows actions whose name or description contains this text.</summary>
    [ObservableProperty]
    private string _searchText = string.Empty;

    /// <summary>The action whose details are shown.</summary>
    [ObservableProperty]
    private ActionItemViewModel? _selected;

    /// <summary>"25 built-in, 1 user action".</summary>
    [ObservableProperty]
    private string _summary = string.Empty;

    /// <summary>Shows what the engine listed.</summary>
    /// <param name="actions">The actions.</param>
    public void Load(IReadOnlyList<ActionInfo> actions)
    {
        ArgumentNullException.ThrowIfNull(actions);
        _all = [.. actions.Select(a => new ActionItemViewModel(a))];
        var user = _all.Count(a => a.IsUserAction);
        Summary = $"{_all.Count - user} built-in, {user} user action{(user == 1 ? string.Empty : "s")}";
        Filter();
        Selected ??= Items.FirstOrDefault();
    }

    partial void OnSearchTextChanged(string value) => Filter();

    private void Filter()
    {
        var search = SearchText.Trim();
        Items.Clear();
        foreach (var item in _all.Where(a => search.Length == 0
            || a.Name.Contains(search, StringComparison.OrdinalIgnoreCase)
            || a.Description.Contains(search, StringComparison.OrdinalIgnoreCase)))
        {
            Items.Add(item);
        }
    }
}
