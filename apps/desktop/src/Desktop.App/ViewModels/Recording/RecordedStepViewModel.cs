// One recorded step on a card in the Record tab: its action, its main values
// in short, the page it acts on or opens, and the reason it is marked for
// review. A step that changes while recording (typing) is replaced in place.

using System.Text.Json;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using Desktop.Protocol.Messages;

namespace Desktop.App.ViewModels.Recording;

/// <summary>A recorded step.</summary>
public sealed partial class RecordedStepViewModel : ObservableObject
{
    private const int MaxDetail = 80;

    /// <summary>Creates the step.</summary>
    /// <param name="index">Its place in the file's steps.</param>
    /// <param name="step">The step as recorded.</param>
    /// <param name="review">Why it is marked for review, or null.</param>
    public RecordedStepViewModel(int index, RecordedStep step, string? review)
    {
        Index = index;
        Update(step, review);
    }

    /// <summary>Its place in the file's steps (0 is the first <c>goto</c>).</summary>
    public int Index { get; }

    /// <summary>The number shown on the card, from 1.</summary>
    public int Number => Index + 1;

    /// <summary>The action, such as <c>click</c>.</summary>
    [ObservableProperty]
    private string _action = string.Empty;

    /// <summary>The parameters in short, such as <c>target: todos.new, value: Buy milk</c>.</summary>
    [ObservableProperty]
    private string _detail = string.Empty;

    /// <summary>"on page receipt" or "opens receipt"; empty for the main page.</summary>
    [ObservableProperty]
    private string _pageText = string.Empty;

    /// <summary>Why the step is marked for review, or null.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasReview))]
    private string? _review;

    /// <summary>The placeholder variable a typed password became, such as <c>loginPassword</c>; null otherwise.</summary>
    [ObservableProperty]
    private string? _passwordVariable;

    /// <summary>The target a step acts on, when it has one.</summary>
    [ObservableProperty]
    private string? _target;

    /// <summary>True when the step is marked for review.</summary>
    public bool HasReview => Review is not null;

    /// <summary>Takes the step as recorded again, such as a <c>fill</c> whose text grew.</summary>
    /// <param name="step">The step.</param>
    /// <param name="review">Why it is marked for review, or null.</param>
    public void Update(RecordedStep step, string? review)
    {
        ArgumentNullException.ThrowIfNull(step);
        Action = step.Action;
        Detail = Shorten(DetailOf(step.Params));
        PageText = step.Opens is { } opens ? $"opens {opens}" : step.Page is { } page && page != "main" ? $"on page {page}" : string.Empty;
        Review = review;
        Target = step.Params.ValueKind == JsonValueKind.Object && step.Params.TryGetProperty("target", out var target) && target.ValueKind == JsonValueKind.String
            ? target.GetString()
            : null;
        PasswordVariable = review is not null
            && step.Action == "fill"
            && step.Params.ValueKind == JsonValueKind.Object
            && step.Params.TryGetProperty("value", out var value)
            && value.ValueKind == JsonValueKind.String
            && Placeholder().Match(value.GetString() ?? string.Empty) is { Success: true } match
                ? match.Groups[1].Value
                : null;
    }

    private static string DetailOf(JsonElement parameters) => parameters.ValueKind switch
    {
        JsonValueKind.Object => string.Join(", ", parameters.EnumerateObject().Select(p => $"{p.Name}: {ValueOf(p.Value)}")),
        JsonValueKind.Undefined or JsonValueKind.Null => string.Empty,
        _ => ValueOf(parameters),
    };

    private static string ValueOf(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => value.GetString() ?? string.Empty,
        _ => value.GetRawText(),
    };

    private static string Shorten(string text) => text.Length <= MaxDetail ? text : text[..(MaxDetail - 1)] + "…";

    [GeneratedRegex(@"^\$\{vars\.([A-Za-z0-9_]+)\}$")]
    private static partial Regex Placeholder();
}

/// <summary>Something the recorder saw but did not record, such as a drag.</summary>
/// <param name="Kind">What kind of thing it was, such as <c>drag</c>.</param>
/// <param name="Message">What the engine says about it.</param>
/// <param name="Where">The page and its address.</param>
public sealed record RecordingNoticeViewModel(string Kind, string Message, string Where);
