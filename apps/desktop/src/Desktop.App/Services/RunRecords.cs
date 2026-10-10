// Reads the records of earlier runs from the project's run folders
// (<project>/<data folder>/runs/<runId>/events.ndjson, ADR 0015 of the
// repository). Only events.ndjson is read: it holds the protocol events as
// sent, while the shape of run.json and test.json is not part of the protocol.
// The engine appends to the file during a run and deletes old run folders, so
// every read expects half-written lines and folders that vanish.

using System.Text.Json;
using Desktop.Protocol;
using Desktop.Protocol.Messages;

namespace Desktop.App.Services;

/// <summary>What the list of earlier runs shows about one run, from its events.</summary>
/// <param name="RunId">The run's id, which is its folder's name and sorts by start time.</param>
/// <param name="Folder">The run's folder.</param>
/// <param name="StartedAt">When it started, from <c>runStarted</c>.</param>
/// <param name="Environment">Its environment, from <c>runStarted</c>.</param>
/// <param name="Status">How it ended, from <c>runFinished</c>; null when its events end without it.</param>
/// <param name="Totals">Its totals, from <c>runFinished</c>.</param>
/// <param name="Problem">Why its events could not be read, or what is wrong with them; null when all is well.</param>
public sealed record RunRecordSummary(
    string RunId,
    string Folder,
    DateTimeOffset? StartedAt,
    string? Environment,
    RunOutcome? Status,
    RunTotals? Totals,
    string? Problem);

/// <summary>The events of one earlier run.</summary>
/// <param name="RunId">The run's id.</param>
/// <param name="Folder">The run's folder.</param>
/// <param name="Events">Its events, in the order of the file.</param>
/// <param name="DamagedLines">How many lines were not events and were left out.</param>
public sealed record RunRecord(string RunId, string Folder, IReadOnlyList<EngineEvent> Events, int DamagedLines);

/// <summary>Reads the records of earlier runs.</summary>
public interface IRunRecords
{
    /// <summary>Lists a project's runs, newest first.</summary>
    /// <param name="root">The project's root folder.</param>
    /// <returns>One summary per run folder; empty when there are none. Never throws for a damaged or vanished folder.</returns>
    IReadOnlyList<RunRecordSummary> List(string root);

    /// <summary>Reads one run's events.</summary>
    /// <param name="root">The project's root folder.</param>
    /// <param name="runId">The run's id.</param>
    /// <returns>The run, or null when its folder or its events file no longer exists.</returns>
    /// <exception cref="IOException">The file exists but cannot be read.</exception>
    RunRecord? Read(string root, string runId);
}

/// <summary>Reads run records from disk.</summary>
public sealed class DiskRunRecords : IRunRecords
{
    /// <summary>The name of the events file in a run folder.</summary>
    public const string EventsFile = "events.ndjson";

    /// <summary>The folder holding a project's run folders.</summary>
    /// <param name="root">The project's root folder.</param>
    /// <returns>Such as <c>/work/shop/.cfe/runs</c>.</returns>
    public static string RunsFolder(string root) => Path.Combine(root, Product.DataDir, "runs");

    /// <inheritdoc />
    public IReadOnlyList<RunRecordSummary> List(string root)
    {
        string[] folders;
        try
        {
            folders = Directory.GetDirectories(RunsFolder(root));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [];
        }

        return [.. folders
            .Select(f => Summarize(Path.GetFileName(f), f))
            .OfType<RunRecordSummary>()
            .OrderByDescending(s => s.RunId, StringComparer.Ordinal)];
    }

    /// <inheritdoc />
    public RunRecord? Read(string root, string runId)
    {
        var folder = Path.Combine(RunsFolder(root), runId);
        var lines = ReadLines(Path.Combine(folder, EventsFile));
        if (lines is null)
        {
            return null;
        }

        var (events, damaged) = Parse(lines);
        return new RunRecord(runId, folder, events, damaged);
    }

    /// <summary>Reads lines of events into events, leaving out lines that are not events.</summary>
    /// <param name="lines">The lines of an events file.</param>
    /// <returns>The events, and how many non-empty lines were left out.</returns>
    /// <example>A last line cut off while the engine was writing it counts as one damaged line.</example>
    public static (IReadOnlyList<EngineEvent> Events, int Damaged) Parse(IEnumerable<string> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);
        var events = new List<EngineEvent>();
        var damaged = 0;
        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (TryRead(line) is { } engineEvent)
            {
                events.Add(engineEvent);
            }
            else
            {
                damaged++;
            }
        }

        return (events, damaged);
    }

    private static RunRecordSummary? Summarize(string runId, string folder)
    {
        List<string>? lines;
        try
        {
            lines = ReadLines(Path.Combine(folder, EventsFile));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new RunRecordSummary(runId, folder, null, null, null, null, $"Its events cannot be read: {ex.Message}");
        }

        if (lines is null)
        {
            // Deleted meanwhile, or a folder the engine did not write.
            return Directory.Exists(folder)
                ? new RunRecordSummary(runId, folder, null, null, null, null, $"It has no {EventsFile}.")
                : null;
        }

        var (events, damaged) = Parse(lines);
        var started = events.OfType<RunStartedEvent>().FirstOrDefault();
        var finished = events.OfType<RunFinishedEvent>().LastOrDefault();
        return new RunRecordSummary(
            runId,
            folder,
            started is null ? null : ParseTime(started.StartedAt),
            started?.Env,
            finished?.Status,
            finished?.Totals,
            damaged == 0 ? null : $"{damaged} {(damaged == 1 ? "line" : "lines")} of its events could not be read.");
    }

    /// <summary>Reads a file's lines while the engine may be appending to it or deleting it; null when it is gone.</summary>
    private static List<string>? ReadLines(string file)
    {
        try
        {
            using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream);
            var lines = new List<string>();
            while (reader.ReadLine() is { } line)
            {
                lines.Add(line);
            }

            return lines;
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            return null;
        }
    }

    private static EngineEvent? TryRead(string line)
    {
        try
        {
            using var document = JsonDocument.Parse(line);
            var message = document.RootElement;
            return message.ValueKind == JsonValueKind.Object
                && message.TryGetProperty("method", out var method) && method.ValueKind == JsonValueKind.String
                && message.TryGetProperty("params", out var parameters) && parameters.ValueKind == JsonValueKind.Object
                ? EngineEvents.Read(method.GetString()!, parameters.Clone())
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static DateTimeOffset? ParseTime(string text) =>
        DateTimeOffset.TryParse(text, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeUniversal, out var t) ? t : null;
}
