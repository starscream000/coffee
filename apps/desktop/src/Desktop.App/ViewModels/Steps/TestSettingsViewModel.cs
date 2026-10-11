// A test's own settings without YAML (instruction D0004, task 19): its name,
// its tags and its skip reason, read from the file's text and written back to
// their own lines when a field loses focus, so the text view's undo covers
// them. Data rows, variables, pages and logins stay in the text view.

using AvaloniaEdit.Document;
using CommunityToolkit.Mvvm.ComponentModel;
using Desktop.App.StepFiles;

namespace Desktop.App.ViewModels.Steps;

/// <summary>A test file's name, tags and skip reason.</summary>
public sealed partial class TestSettingsViewModel : ObservableObject
{
    private readonly string _file;
    private readonly TextDocument _document;
    private bool _reading;
    private bool _writing;

    /// <summary>Creates the settings and reads them, and again whenever the text changes.</summary>
    /// <param name="file">The test file, for its kind.</param>
    /// <param name="document">The text, shared with the text view.</param>
    public TestSettingsViewModel(string file, TextDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        _file = file;
        _document = document;
        _document.TextChanged += (_, _) =>
        {
            if (!_writing)
            {
                Read();
            }
        };
        Read();
    }

    /// <summary>The test's name.</summary>
    [ObservableProperty]
    private string _name = string.Empty;

    /// <summary>The tags, separated by commas.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TagList), nameof(HasTags))]
    private string _tags = string.Empty;

    /// <summary>Why the test is skipped; empty when it runs.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSkipped))]
    private string _skip = string.Empty;

    /// <summary>What is wrong with the name, or null.</summary>
    [ObservableProperty]
    private string? _nameError;

    /// <summary>Why the settings cannot be shown (the file cannot be read), or null.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAvailable))]
    private string? _problem;

    /// <summary>True when the settings can be edited here.</summary>
    public bool IsAvailable => Problem is null;

    /// <summary>The tags, one by one.</summary>
    public IReadOnlyList<string> TagList => SplitTags(Tags);

    /// <summary>True when the test has tags.</summary>
    public bool HasTags => TagList.Count > 0;

    /// <summary>True when the test is skipped.</summary>
    public bool IsSkipped => Skip.Trim().Length > 0;

    /// <summary>What the form says about the rest of a test.</summary>
    public static string Rest => "Data rows, variables, pages and logins are edited in the Text view.";

    /// <summary>Tags as typed, split at commas, without blanks or repeats.</summary>
    /// <param name="text">Such as <c>smoke, checkout</c>.</param>
    /// <returns>The tags.</returns>
    public static IReadOnlyList<string> SplitTags(string text) =>
        [.. (text ?? string.Empty).Split(',').Select(t => t.Trim()).Where(t => t.Length > 0).Distinct(StringComparer.Ordinal)];

    /// <summary>Reads the settings from the text again.</summary>
    public void Read()
    {
        var outline = StepOutline.Read(_file, _document.Text);
        _reading = true;
        try
        {
            if (outline.Root is not { } root)
            {
                Problem = $"The test's settings cannot be shown. {outline.Problem} Edit it as text.";
                return;
            }

            Problem = null;
            NameError = null;
            Name = root.Get("name") is YamlScalar name ? name.Value : string.Empty;
            Tags = root.Get("tags") switch
            {
                YamlSequence sequence => string.Join(", ", sequence.Items.OfType<YamlScalar>().Select(s => s.Value)),
                YamlScalar { IsNull: false } one => one.Value,
                _ => string.Empty,
            };
            Skip = root.Get("skip") is YamlScalar { IsNull: false } skip ? skip.Value : string.Empty;
        }
        finally
        {
            _reading = false;
        }
    }

    partial void OnNameChanged(string value)
    {
        if (_reading)
        {
            return;
        }

        var name = value.Trim();
        NameError = name.Length == 0 ? "A test needs a name. The old name is kept until you give one." : null;
        if (NameError is null)
        {
            Write("name", StepWriter.Text(name), "version");
        }
    }

    partial void OnTagsChanged(string value)
    {
        if (!_reading)
        {
            var tags = SplitTags(value);
            Write("tags", tags.Count == 0 ? null : "[" + string.Join(", ", tags.Select(StepWriter.Text)) + "]", "description", "name", "version");
        }
    }

    partial void OnSkipChanged(string value)
    {
        if (!_reading)
        {
            var reason = value.Trim();
            Write("skip", reason.Length == 0 ? null : StepWriter.Text(reason), "tags", "description", "name", "version");
        }
    }

    private void Write(string key, string? yaml, params string[] after)
    {
        var outline = StepOutline.Read(_file, _document.Text);
        if (outline.Root is not { } root)
        {
            return;
        }

        _writing = true;
        try
        {
            DocumentText.Replace(_document, RootKeys.Set(outline.Text, root, key, yaml, outline.NewLine, after));
        }
        finally
        {
            _writing = false;
        }
    }
}
