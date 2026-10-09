// The product's names for the desktop app: the C# copy of PRODUCT in
// packages/protocol/src/product.ts. Code never writes a product name literally;
// it uses these. ProductTests fails when this copy and product.ts disagree.

namespace Desktop.Protocol;

/// <summary>
/// The product's four independent names (ADR 0019 of the repository) and the
/// values derived from them. Use these instead of writing a name literally.
/// </summary>
/// <example>
/// <code>
/// var configPath = Path.Combine(root, Product.ConfigFile);
/// </code>
/// </example>
public static class Product
{
    /// <summary>Name shown to people: window titles, dialogs, documentation.</summary>
    public const string DisplayName = "Coffee";

    /// <summary>Name of the command-line program. Lower case, letters, digits and <c>-</c>.</summary>
    public const string Command = "cfe";

    /// <summary>Git-ignored folder in the user's repository for runs, logins and caches.</summary>
    public const string DataDir = ".cfe";

    /// <summary>npm scope of every package of the product.</summary>
    public const string NpmScope = "@cfe";

    /// <summary>Project configuration file at the root of the user's repository.</summary>
    public static string ConfigFile => $"{Command}.config.yaml";

    /// <summary>Prefix of the product's own environment variables, such as <c>CFE_ENGINE</c>.</summary>
    public static string EnvPrefix => $"{Command.ToUpperInvariant().Replace('-', '_')}_";

    /// <summary>Name this client sends to the engine in <c>initialize</c>.</summary>
    public static string ClientName => $"{NpmScope}/desktop";
}
