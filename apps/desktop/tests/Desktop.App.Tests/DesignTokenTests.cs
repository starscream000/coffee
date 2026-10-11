// Checks the rule of ADR D0008: no colour, size or spacing value is written in
// a view. Every view (.axaml under Views/ and the main window) takes colours,
// text sizes, spacing, corner radii and sizes from the design tokens in
// Themes/Tokens.axaml, by resource or by style class; its code-behind builds
// no colour either.

using System.Text.RegularExpressions;
using System.Xml.Linq;
using Desktop.Protocol.Tests;

namespace Desktop.App.Tests;

public sealed partial class DesignTokenTests
{
    /// <summary>Properties that take a colour.</summary>
    private static readonly HashSet<string> ColourProperties =
        ["Foreground", "Background", "BorderBrush", "Fill", "Stroke", "Color", "CaretBrush", "SelectionBrush"];

    /// <summary>Properties that take a size or a spacing.</summary>
    private static readonly HashSet<string> SizeProperties =
    [
        "FontSize", "Margin", "Padding", "Spacing", "CornerRadius", "BorderThickness",
        "Width", "Height", "MinWidth", "MinHeight", "MaxWidth", "MaxHeight",
    ];

    /// <summary>Grid definitions may only share out space (<c>*</c>, <c>2*</c>, <c>Auto</c>), never fix a size.</summary>
    private static readonly HashSet<string> GridProperties = ["ColumnDefinitions", "RowDefinitions"];

    private static string ViewsFolder => RepoPaths.Of("apps/desktop/src/Desktop.App/Views");

    public static TheoryData<string> ViewFiles =>
        [.. Directory.EnumerateFiles(ViewsFolder, "*.axaml", SearchOption.AllDirectories).Select(path => Path.GetFileName(path)).Order(StringComparer.Ordinal)];

    [Theory]
    [MemberData(nameof(ViewFiles))]
    public void A_view_takes_colours_sizes_and_spacing_from_the_tokens(string file)
    {
        var problems = new List<string>();
        var document = XDocument.Load(Path.Combine(ViewsFolder, file), LoadOptions.SetLineInfo);
        foreach (var element in document.Descendants())
        {
            var line = ((System.Xml.IXmlLineInfo)element).LineNumber;
            foreach (var attribute in element.Attributes())
            {
                var name = attribute.Name.LocalName;
                // Attached properties such as Grid.Row are not values of this kind.
                var property = name.Contains('.', StringComparison.Ordinal) ? name[(name.LastIndexOf('.') + 1)..] : name;
                CheckValue(property, attribute.Value, line, problems);
            }

            // A local style's setter counts as a value written in the view.
            if (element.Name.LocalName == "Setter"
                && element.Attribute("Property")?.Value is { } setterProperty
                && element.Attribute("Value")?.Value is { } setterValue)
            {
                CheckValue(setterProperty, setterValue, line, problems);
            }
        }

        Assert.True(problems.Count == 0, $"{file}:{Environment.NewLine}{string.Join(Environment.NewLine, problems)}");
    }

    [Fact]
    public void A_views_code_builds_no_colour()
    {
        var problems = Directory.EnumerateFiles(ViewsFolder, "*.cs", SearchOption.AllDirectories)
            .SelectMany(path => File.ReadLines(path).Select((text, index) => (path, text, index)))
            .Where(line => ColourInCode().IsMatch(line.text))
            .Select(line => $"{Path.GetFileName(line.path)}:{line.index + 1}: {line.text.Trim()}")
            .ToList();
        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    [Fact]
    public void Every_token_a_view_names_exists()
    {
        var tokens = XDocument.Load(RepoPaths.Of("apps/desktop/src/Desktop.App/Themes/Tokens.axaml"))
            .Descendants()
            .Select(e => e.Attribute(XName.Get("Key", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value)
            .OfType<string>()
            .ToHashSet(StringComparer.Ordinal);
        var used = Directory.EnumerateFiles(ViewsFolder, "*.axaml", SearchOption.AllDirectories)
            .Concat(Directory.EnumerateFiles(RepoPaths.Of("apps/desktop/src/Desktop.App/Themes"), "*.axaml"))
            .SelectMany(path => ResourceReference().Matches(File.ReadAllText(path)).Select(m => m.Groups[1].Value))
            .ToHashSet(StringComparer.Ordinal);
        Assert.Empty(used.Except(tokens).Order(StringComparer.Ordinal));
    }

    private static void CheckValue(string property, string value, int line, List<string> problems)
    {
        if (value.StartsWith('{'))
        {
            return;
        }

        if (ColourProperties.Contains(property))
        {
            problems.Add($"  line {line}: {property}=\"{value}\" writes a colour; use a token");
        }
        else if (SizeProperties.Contains(property) && value is not ("0" or "Auto" or "NaN") && !SharedSpace().IsMatch(value))
        {
            problems.Add($"  line {line}: {property}=\"{value}\" writes a size or spacing; use a token");
        }
        else if (GridProperties.Contains(property) && value.Split(',').Any(part => !SharedSpace().IsMatch(part.Trim())))
        {
            problems.Add($"  line {line}: {property}=\"{value}\" fixes a size; share out space with * and Auto, or use a token");
        }
    }

    [GeneratedRegex(@"^(Auto|\d*\*)$")]
    private static partial Regex SharedSpace();

    [GeneratedRegex(@"Color\.From|new\s+SolidColorBrush|Colors\.|Brushes\.|""#[0-9A-Fa-f]{3,8}""")]
    private static partial Regex ColourInCode();

    [GeneratedRegex(@"\{(?:StaticResource|DynamicResource) (\w+)\}")]
    private static partial Regex ResourceReference();
}
