// The protocol's closed sets of string values, as C# enums. Each has Unknown = 0
// for a value from a newer engine (ProtocolEnumConverter).

using System.Text.Json.Serialization;
using Desktop.Protocol.Json;

namespace Desktop.Protocol.Messages;

/// <summary>How serious a diagnostic is.</summary>
[JsonConverter(typeof(ProtocolEnumConverter<DiagnosticSeverity>))]
public enum DiagnosticSeverity
{
    /// <summary>A value this client does not know.</summary>
    Unknown = 0,
    /// <summary>The file cannot run until this is fixed.</summary>
    Error,
    /// <summary>The file runs, but something looks wrong.</summary>
    Warning,
}

/// <summary>The section of a test or flow a step belongs to.</summary>
[JsonConverter(typeof(ProtocolEnumConverter<StepSection>))]
public enum StepSection
{
    /// <summary>A value this client does not know.</summary>
    Unknown = 0,
    /// <summary>Steps that prepare the test.</summary>
    Before,
    /// <summary>The test's main steps.</summary>
    Steps,
    /// <summary>Clean-up steps that always run.</summary>
    After,
}

/// <summary>Outcome of a test or of a whole run.</summary>
[JsonConverter(typeof(ProtocolEnumConverter<RunOutcome>))]
public enum RunOutcome
{
    /// <summary>A value this client does not know.</summary>
    Unknown = 0,
    /// <summary>Everything passed.</summary>
    Passed,
    /// <summary>At least one step or test failed.</summary>
    Failed,
    /// <summary>The run was cancelled.</summary>
    Cancelled,
}

/// <summary>Level of a log event.</summary>
[JsonConverter(typeof(ProtocolEnumConverter<LogLevel>))]
public enum LogLevel
{
    /// <summary>A value this client does not know.</summary>
    Unknown = 0,
    /// <summary>Detail for developers.</summary>
    Debug,
    /// <summary>Information.</summary>
    Info,
    /// <summary>Something the user should look at; the run goes on.</summary>
    Warn,
    /// <summary>Something went wrong.</summary>
    Error,
}

/// <summary>Why a step was skipped.</summary>
[JsonConverter(typeof(ProtocolEnumConverter<StepSkipReason>))]
public enum StepSkipReason
{
    /// <summary>A value this client does not know.</summary>
    Unknown = 0,
    /// <summary>An earlier step failed.</summary>
    PreviousFailure,
    /// <summary>The run was cancelled.</summary>
    Cancelled,
    /// <summary>An <c>after</c> step used a variable that was never set.</summary>
    VariableNotSet,
}

/// <summary>What happened to a step's page snapshot.</summary>
[JsonConverter(typeof(ProtocolEnumConverter<SnapshotState>))]
public enum SnapshotState
{
    /// <summary>A value this client does not know.</summary>
    Unknown = 0,
    /// <summary>The snapshot was saved; <c>snapshotReady</c> was sent.</summary>
    Saved,
    /// <summary>No snapshot was wanted for this step.</summary>
    Skipped,
    /// <summary>Recording or masking failed; only the screenshot exists.</summary>
    Failed,
}

/// <summary>Where an action is defined.</summary>
[JsonConverter(typeof(ProtocolEnumConverter<ActionSourceKind>))]
public enum ActionSourceKind
{
    /// <summary>A value this client does not know.</summary>
    Unknown = 0,
    /// <summary>A built-in action of the engine.</summary>
    Builtin,
    /// <summary>A user action in a file of the project.</summary>
    File,
}
