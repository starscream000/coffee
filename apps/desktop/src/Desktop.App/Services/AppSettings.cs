// The app's settings and where they are stored: a JSON file in the user's
// application data folder, under the product's display name.

using System.Text.Json;
using Desktop.Protocol;

namespace Desktop.App.Services;

/// <summary>Settings the user can change, and the recent projects.</summary>
public sealed record AppSettings
{
    /// <summary>Most recent projects kept.</summary>
    public const int MaxRecentProjects = 10;

    /// <summary>Path of the Node executable; null to search (ADR D0004).</summary>
    public string? NodePath { get; init; }

    /// <summary>Path of the engine's <c>dist/main.js</c>; null to search.</summary>
    public string? EnginePath { get; init; }

    /// <summary>Recently opened project folders, newest first.</summary>
    public IReadOnlyList<string> RecentProjects { get; init; } = [];

    /// <summary>Returns these settings with <paramref name="root"/> first in the recent projects.</summary>
    /// <param name="root">The project folder just opened.</param>
    /// <returns>The new settings.</returns>
    public AppSettings WithRecentProject(string root) => this with
    {
        RecentProjects = [root, .. RecentProjects.Where(p => !string.Equals(p, root, StringComparison.Ordinal)).Take(MaxRecentProjects - 1)],
    };
}

/// <summary>Loads and saves <see cref="AppSettings"/>.</summary>
public interface ISettingsStore
{
    /// <summary>Loads the settings; defaults when there are none or the file cannot be read.</summary>
    /// <returns>The settings.</returns>
    AppSettings Load();

    /// <summary>Saves the settings.</summary>
    /// <param name="settings">The settings.</param>
    /// <exception cref="IOException">The file could not be written.</exception>
    void Save(AppSettings settings);
}

/// <summary>Stores the settings as JSON in a file.</summary>
/// <param name="path">The settings file.</param>
public sealed class JsonSettingsStore(string path) : ISettingsStore
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    /// <summary>The settings file in this user's application data folder.</summary>
    public static string DefaultPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), Product.DisplayName, "desktop-settings.json");

    /// <inheritdoc />
    public AppSettings Load()
    {
        try
        {
            return File.Exists(path)
                ? JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path), Options) ?? new AppSettings()
                : new AppSettings();
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return new AppSettings();
        }
    }

    /// <inheritdoc />
    public void Save(AppSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(settings, Options));
    }
}
