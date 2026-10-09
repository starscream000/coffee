// Tests against the real engine of this checkout, through a real child
// process: the handshake, the requests the engine implements today, the
// refusal of an incompatible client, and a clean shutdown.

using Desktop.Protocol;
using Desktop.Protocol.Messages;

namespace Desktop.Engine.Tests;

public sealed class RealEngineTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Handshake_reports_the_engine_and_its_protocol_version()
    {
        await using var session = await RealEngine.StartAsync();
        Assert.Equal(EngineState.Ready, session.State);
        Assert.Equal(Product.NpmScope + "/engine", session.Engine!.Engine.Name);
        Assert.True(ProtocolVersion.IsCompatible(ProtocolVersion.Current, session.Engine.ProtocolVersion));
    }

    [Fact]
    public async Task Opens_the_demo_project()
    {
        await using var session = await RealEngine.StartAsync();
        var project = await session.Client.OpenProjectAsync(RealEngine.DemoApp, Ct);
        Assert.Contains("local", project.Environments);
        Assert.Equal("local", project.DefaultEnvironment);
        Assert.EndsWith(Product.ConfigFile, project.ConfigFile, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Validates_a_file_with_errors_and_reports_file_and_line()
    {
        await using var session = await RealEngine.StartAsync();
        await session.Client.OpenProjectAsync(RealEngine.DemoApp, Ct);
        const string file = "fixtures/invalid/schema-errors.test.yaml";

        var result = await session.Client.ValidateFilesAsync([file], Ct);

        Assert.NotEmpty(result.Diagnostics);
        Assert.All(result.Diagnostics, d =>
        {
            Assert.Equal(file, d.File);
            Assert.True(d.Line >= 1 && d.Column >= 1);
            Assert.NotEqual(DiagnosticSeverity.Unknown, d.Severity);
        });
    }

    [Fact]
    public async Task Validates_an_unsaved_buffer()
    {
        await using var session = await RealEngine.StartAsync();
        await session.Client.OpenProjectAsync(RealEngine.DemoApp, Ct);
        var result = await session.Client.ValidateContentAsync("tests/new.test.yaml", "version: 1\nname: New\nsteps:\n  - nosuchaction: x\n", Ct);
        Assert.Contains(result.Diagnostics, d => d.Severity == DiagnosticSeverity.Error && d.Line == 4);
    }

    [Fact]
    public async Task Lists_built_in_and_user_actions()
    {
        await using var session = await RealEngine.StartAsync();
        await session.Client.OpenProjectAsync(RealEngine.DemoApp, Ct);
        var actions = (await session.Client.ListActionsAsync(Ct)).Actions;
        Assert.Contains(actions, a => a.Name == "goto" && a.Source.Kind == ActionSourceKind.Builtin);
        Assert.Contains(actions, a => a.Name == "demo.addTodo" && a.Source.Kind == ActionSourceKind.File && a.Shorthand == "title");
    }

    [Fact]
    public async Task ListTests_answers_or_says_it_is_not_implemented_yet()
    {
        await using var session = await RealEngine.StartAsync();
        await session.Client.OpenProjectAsync(RealEngine.DemoApp, Ct);
        try
        {
            var tests = await session.Client.ListTestsAsync(cancellationToken: Ct);
            Assert.Contains(tests.Tests, t => t.File == "tests/user-action.test.yaml");
        }
        catch (EngineRequestException ex)
        {
            Assert.Equal(ErrorCodes.MethodNotFound, ex.Name);
        }
    }

    [Fact]
    public async Task A_project_request_before_openProject_is_ProjectNotOpen()
    {
        await using var session = await RealEngine.StartAsync();
        var ex = await Assert.ThrowsAsync<EngineRequestException>(() => session.Client.ValidateFilesAsync(["a.test.yaml"], Ct));
        Assert.Equal(ErrorCodes.ProjectNotOpen, ex.Name);
    }

    [Fact]
    public async Task A_folder_without_config_is_ProjectInvalid()
    {
        await using var session = await RealEngine.StartAsync();
        var empty = Directory.CreateTempSubdirectory().FullName;
        try
        {
            var ex = await Assert.ThrowsAsync<EngineRequestException>(() => session.Client.OpenProjectAsync(empty, Ct));
            Assert.Equal(ErrorCodes.ProjectInvalid, ex.Name);
        }
        finally
        {
            Directory.Delete(empty, recursive: true);
        }
    }

    [Fact]
    public async Task Refuses_an_incompatible_client_and_exits_with_code_3()
    {
        var engine = EngineProcess.Start(RealEngine.LaunchOrSkip(), _ => { });
        await using var _ = engine;
        await using var connection = new JsonRpcConnection(engine);
        connection.Start();

        var ex = await Assert.ThrowsAsync<EngineRequestException>(() => connection.SendAsync<InitializeParams, InitializeResult>(
            Methods.Initialize,
            new InitializeParams { ProtocolVersion = "99.0.0", Client = new SoftwareInfo { Name = Product.ClientName, Version = "0" } },
            Ct));

        Assert.Equal(ErrorCodes.IncompatibleProtocol, ex.Name);
        Assert.Equal("99.0.0", ex.DataAs<IncompatibleProtocolData>()!.ClientProtocolVersion);
        Assert.Equal(3, await engine.Exited.WaitAsync(TimeSpan.FromSeconds(10), Ct));
    }

    [Fact]
    public async Task Stops_cleanly_with_exit_code_0()
    {
        EngineProcess? process = null;
        await using var session = new EngineSession((launch, onLine) => process = EngineProcess.Start(launch, onLine));
        await session.StartAsync(RealEngine.LaunchOrSkip(), "0.0.0-test", Ct);
        var exited = process!.Exited;

        await session.StopAsync(cancellationToken: Ct);

        Assert.Equal(0, await exited);
        Assert.Equal(EngineState.Stopped, session.State);
    }
}
