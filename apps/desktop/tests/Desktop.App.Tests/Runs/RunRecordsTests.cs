// Reading earlier runs from their folders (instruction D0003, tasks 12 and
// 13): newest first with what runStarted and runFinished say; only
// events.ndjson is read; damaged and half-written lines, unknown events,
// folders without events and folders that vanish do not break the list.

using Desktop.App.Services;
using Desktop.App.Tests.Fakes;
using Desktop.Protocol.Messages;

namespace Desktop.App.Tests.Runs;

public sealed class RunRecordsTests : IDisposable
{
    private readonly RunFolders _runs = new();
    private readonly DiskRunRecords _records = new();

    public void Dispose() => _runs.Dispose();

    [Fact]
    public void A_project_without_runs_lists_none()
    {
        Assert.Empty(_records.List(_runs.Root));
        Assert.Empty(_records.List(Path.Combine(_runs.Root, "missing")));
    }

    [Fact]
    public void Runs_are_listed_newest_first_with_what_their_events_say()
    {
        _runs.WritePassed("20261009-090000-000-aaaa", env: "staging");
        _runs.WritePassed("20261010-090000-000-bbbb");
        var s = new RunScript("20261010-100000-000-cccc");
        _runs.Write(s.RunId, s.Started(RunScript.Planned("t1", "tests/a.test.yaml", "A")), s.TestStarted("t1"));

        var list = _records.List(_runs.Root);

        Assert.Equal(["20261010-100000-000-cccc", "20261010-090000-000-bbbb", "20261009-090000-000-aaaa"], list.Select(r => r.RunId));
        Assert.Null(list[0].Status);
        Assert.Equal(RunOutcome.Passed, list[1].Status);
        Assert.Equal(1, list[1].Totals!.Passed);
        Assert.Equal("staging", list[2].Environment);
        Assert.Equal(new DateTimeOffset(2026, 10, 10, 9, 15, 0, TimeSpan.Zero), list[1].StartedAt);
        Assert.All(list, r => Assert.Null(r.Problem));
    }

    [Fact]
    public void Only_events_ndjson_is_read()
    {
        var folder = _runs.WritePassed("20261010-090000-000-bbbb");
        File.WriteAllText(Path.Combine(folder, "run.json"), "{ not the shape you expect");

        var record = _records.Read(_runs.Root, "20261010-090000-000-bbbb")!;

        Assert.Equal(6, record.Events.Count);
        Assert.Equal(0, record.DamagedLines);
    }

    [Fact]
    public void Damaged_and_half_written_lines_are_counted_and_left_out()
    {
        var s = new RunScript("r1");
        var finished = RunFolders.Line(s.Started());
        _runs.Write(
            "r1",
            finished,
            "not json at all",
            """{"jsonrpc":"2.0","method":"testStarted","params":{"runId":"r1"}}""",
            """{"jsonrpc":"2.0","method":"futureEvent","params":{"runId":"r1","seq":3}}""",
            "",
            """{"jsonrpc":"2.0","method":"runFinish""");

        var record = _records.Read(_runs.Root, "r1")!;
        var summary = Assert.Single(_records.List(_runs.Root));

        Assert.Equal(3, record.DamagedLines);
        Assert.IsType<RunStartedEvent>(record.Events[0]);
        Assert.Equal("futureEvent", Assert.IsType<UnknownEngineEvent>(record.Events[1]).Method);
        Assert.Equal("3 lines of its events could not be read.", summary.Problem);
        Assert.Null(summary.Status);
    }

    [Fact]
    public void A_folder_without_events_is_listed_with_the_reason()
    {
        Directory.CreateDirectory(_runs.Folder("20261010-090000-000-dddd"));

        var summary = Assert.Single(_records.List(_runs.Root));

        Assert.Equal("It has no events.ndjson.", summary.Problem);
        Assert.Null(_records.Read(_runs.Root, "20261010-090000-000-dddd"));
    }

    [Fact]
    public void A_run_deleted_after_listing_reads_as_gone()
    {
        var folder = _runs.WritePassed("20261010-090000-000-bbbb");
        Assert.Single(_records.List(_runs.Root));

        Directory.Delete(folder, recursive: true);

        Assert.Null(_records.Read(_runs.Root, "20261010-090000-000-bbbb"));
        Assert.Empty(_records.List(_runs.Root));
    }

    [Fact]
    public void A_file_the_engine_is_still_writing_can_be_read()
    {
        var s = new RunScript("r1");
        var folder = _runs.Write("r1", s.Started());
        using var writer = new FileStream(Path.Combine(folder, DiskRunRecords.EventsFile), FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
        writer.Write("""{"jsonrpc":"2.0","method":"testSta"""u8);
        writer.Flush();

        var record = _records.Read(_runs.Root, "r1")!;

        Assert.Single(record.Events);
        Assert.Equal(1, record.DamagedLines);
    }
}
