// Maps every JSON Schema file of packages/protocol/schema/ to the C# type that
// represents it. The contract tests use this table to compare the two
// (ADR D0003).

using Desktop.Protocol.Messages;

namespace Desktop.Protocol;

/// <summary>The C# type of each protocol schema, by schema key.</summary>
public static class ProtocolTypes
{
    /// <summary>
    /// Schema key (the file name without <c>.json</c>, such as
    /// <c>request.openProject.result</c>) to C# type.
    /// </summary>
    public static IReadOnlyDictionary<string, Type> BySchemaKey { get; } = Build();

    private static Dictionary<string, Type> Build()
    {
        var map = new Dictionary<string, Type>(StringComparer.Ordinal)
        {
            ["envelope.request"] = typeof(JsonRpcRequest),
            ["envelope.notification"] = typeof(JsonRpcNotification),
            ["envelope.success-response"] = typeof(JsonRpcSuccessResponse),
            ["envelope.error-response"] = typeof(JsonRpcErrorResponse),
            ["error-data.ErrorData"] = typeof(ErrorData),
            ["error-data.IncompatibleProtocol"] = typeof(IncompatibleProtocolData),
            ["error-data.StepFilesInvalid"] = typeof(StepFilesInvalidData),
            ["error-data.SnapshotUnavailable"] = typeof(SnapshotUnavailableData),
            ["type.Location"] = typeof(Location),
            ["type.Diagnostic"] = typeof(Diagnostic),
            ["type.LocatorUse"] = typeof(LocatorUse),
            ["type.SnapshotStatus"] = typeof(SnapshotStatus),
            ["type.ErrorInfo"] = typeof(ErrorInfo),
            ["request.initialize.params"] = typeof(InitializeParams),
            ["request.initialize.result"] = typeof(InitializeResult),
            ["request.shutdown.params"] = typeof(EmptyParams),
            ["request.shutdown.result"] = typeof(NullResult),
            ["request.openProject.params"] = typeof(OpenProjectParams),
            ["request.openProject.result"] = typeof(OpenProjectResult),
            ["request.listTests.params"] = typeof(ListTestsParams),
            ["request.listTests.result"] = typeof(ListTestsResult),
            ["request.listActions.params"] = typeof(EmptyParams),
            ["request.listActions.result"] = typeof(ListActionsResult),
            ["request.validate.params"] = typeof(ValidateParams),
            ["request.validate.result"] = typeof(ValidateResult),
            ["request.startRun.params"] = typeof(StartRunParams),
            ["request.startRun.result"] = typeof(StartRunResult),
            ["request.cancelRun.params"] = typeof(CancelRunParams),
            ["request.cancelRun.result"] = typeof(NullResult),
            ["request.openSnapshot.params"] = typeof(OpenSnapshotParams),
            ["request.openSnapshot.result"] = typeof(NullResult),
        };
        foreach (var (method, type) in EngineEvents.ByMethod)
        {
            map[$"event.{method}"] = type;
        }

        return map;
    }
}
