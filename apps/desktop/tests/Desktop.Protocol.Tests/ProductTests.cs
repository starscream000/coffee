// Checks that the C# product names equal PRODUCT in packages/protocol/src/product.ts,
// so renaming the product stays one change in TypeScript plus this file.

using System.Text.RegularExpressions;

namespace Desktop.Protocol.Tests;

public sealed partial class ProductTests
{
    private static readonly string ProductTs =
        File.ReadAllText(RepoPaths.Of("packages/protocol/src/product.ts"));

    [Theory]
    [InlineData("displayName", Product.DisplayName)]
    [InlineData("command", Product.Command)]
    [InlineData("dataDir", Product.DataDir)]
    [InlineData("npmScope", Product.NpmScope)]
    public void Name_equals_the_TypeScript_PRODUCT(string key, string value)
    {
        var match = Regex.Match(ProductTs, $@"^\s*{key}:\s*'([^']*)',", RegexOptions.Multiline);
        Assert.True(match.Success, $"product.ts has no '{key}' entry");
        Assert.Equal(match.Groups[1].Value, value);
    }

    [Fact]
    public void Derived_names_follow_the_TypeScript_rules()
    {
        Assert.Equal($"{Product.Command}.config.yaml", Product.ConfigFile);
        Assert.Matches("^[A-Z][A-Z0-9_]*_$", Product.EnvPrefix);
        Assert.StartsWith(Product.NpmScope + "/", Product.ClientName, StringComparison.Ordinal);
    }
}
