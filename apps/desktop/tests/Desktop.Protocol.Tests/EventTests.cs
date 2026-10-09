// Tests of reading events and of the tolerance rules for newer engines:
// unknown fields, unknown events and unknown enum values.

using System.Text.Json;
using Desktop.Protocol.Json;
using Desktop.Protocol.Messages;

namespace Desktop.Protocol.Tests;

public sealed class EventTests
{
    private static JsonElement Json(string text) => JsonDocument.Parse(text).RootElement.Clone();

    [Fact]
    public void Reads_a_known_event_as_its_type()
    {
        var engineEvent = EngineEvents.Read("testFinished", Json("""{"runId":"r","seq":4,"testId":"a#0","status":"failed","durationMs":12.5}"""));
        var finished = Assert.IsType<TestFinishedEvent>(engineEvent);
        Assert.Equal(RunOutcome.Failed, finished.Status);
        Assert.Equal(4, finished.Seq);
    }

    [Fact]
    public void Ignores_unknown_fields()
    {
        var engineEvent = EngineEvents.Read("testStarted", Json("""{"runId":"r","seq":1,"testId":"a#0","startedAt":"t","futureField":[1,2]}"""));
        Assert.IsType<TestStartedEvent>(engineEvent);
    }

    [Fact]
    public void Passes_an_unknown_event_on_with_its_run_and_seq()
    {
        var engineEvent = EngineEvents.Read("videoReady", Json("""{"runId":"r","seq":9,"path":"/v.webm"}"""));
        var unknown = Assert.IsType<UnknownEngineEvent>(engineEvent);
        Assert.Equal(("videoReady", "r", 9), (unknown.Method, unknown.RunId, unknown.Seq));
    }

    [Fact]
    public void Reads_an_unknown_enum_value_as_Unknown()
    {
        var engineEvent = EngineEvents.Read("log", Json("""{"runId":"r","seq":2,"level":"trace","message":"m"}"""));
        Assert.Equal(LogLevel.Unknown, Assert.IsType<LogEvent>(engineEvent).Level);
    }

    [Fact]
    public void A_known_event_without_a_required_field_is_an_error()
    {
        Assert.ThrowsAny<JsonException>(() => EngineEvents.Read("testStarted", Json("""{"runId":"r","seq":1}""")));
    }

    [Fact]
    public void Writes_enums_in_camelCase_and_refuses_to_write_Unknown()
    {
        Assert.Equal("\"previousFailure\"", JsonSerializer.Serialize(StepSkipReason.PreviousFailure, ProtocolJson.Options));
        Assert.Throws<JsonException>(() => JsonSerializer.Serialize(StepSkipReason.Unknown, ProtocolJson.Options));
    }

    [Fact]
    public void Writes_null_for_a_required_nullable_field_and_omits_absent_optional_ones()
    {
        var use = new LocatorUse { Param = "target", CandidateIndex = null, Candidate = null };
        Assert.Equal("""{"param":"target","candidateIndex":null,"candidate":null}""", JsonSerializer.Serialize(use, ProtocolJson.Options));
    }
}
