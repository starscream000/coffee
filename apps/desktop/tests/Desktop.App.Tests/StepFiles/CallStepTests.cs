// A call step picks its flow (instruction D0004, task 20): the flow field
// offers the project's flows, the chosen flow's parameters are fields of
// "with", values are written back as one mapping, a flow changed keeps only
// the new flow's parameters, and a flow that cannot be read leaves "with" as
// YAML. With DESKTOP_SCREENSHOTS set to a folder, the view test also saves
// what it rendered.

using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Desktop.App.Services;
using Desktop.App.StepFiles;
using Desktop.App.Tests.Fakes;
using Desktop.App.ViewModels;
using Desktop.App.ViewModels.Steps;
using Desktop.App.Views;

namespace Desktop.App.Tests.StepFiles;

public sealed class CallStepTests
{
    private const string Root = "/work/shop-tests";
    private const string Test = "tests/a.test.yaml";
    private const string Checkout = "flows/checkout.flow.yaml";
    private const string Login = "flows/log-in.flow.yaml";

    private const string Text = """
        version: 1
        name: A
        steps:
          - goto: /products
          - call:
              flow: flows/checkout.flow.yaml
              with:
                email: guest@example.com
        """;

    private static async Task<(ShellViewModel Shell, StepFileViewModel Tab, FakeProjectFiles Files)> OpenAsync(string text = Text)
    {
        var engine = new FakeEngineService { Tests = [Make.Test(Test, "A")], Actions = StepFormTests.RealActions() };
        var files = new FakeProjectFiles();
        files.Files[Test] = text + "\n";
        files.Files[Checkout] = "version: 1\nname: Check out\nparams:\n  email: { type: string }\n  express: { type: boolean, default: false }\nsteps:\n  - goto: /checkout\n";
        files.Files[Login] = "version: 1\nname: Log in\nparams:\n  user: { type: string }\n  attempts: { type: number, default: 1 }\nsteps:\n  - goto: /login\n";
        var shell = new ShellViewModel(engine, new MemorySettingsStore(), new FakeFolderPicker(Root), files, new ImmediateDispatcher(), p => p == Root, new FakeDialogs(), new ManualDelay());
        await shell.OpenProjectAsync(Root);
        shell.Workspace!.OpenFile(Test);
        return (shell, (StepFileViewModel)shell.Workspace.SelectedTab!, files);
    }

    private static StepFormViewModel SelectCall(StepFileViewModel tab)
    {
        tab.Steps!.SelectCommand.Execute(tab.Steps.Sections[1].Steps.First(s => s.Action == "call"));
        return tab.Steps.Form!;
    }

    [Fact]
    public void A_flows_parameters_are_read_with_their_types_and_defaults()
    {
        var fields = FlowParams.Read("version: 1\nparams:\n  email: { type: string }\n  express: { type: boolean, default: false }\n  count: { type: number }\nsteps: []\n")!;

        Assert.Equal(["email", "express", "count"], fields.Select(f => f.Name));
        Assert.Equal([FieldKind.Text, FieldKind.Boolean, FieldKind.Number], fields.Select(f => f.Kind));
        Assert.Equal([true, false, true], fields.Select(f => f.IsRequired));
        Assert.Equal("The flow's boolean parameter; false when not given.", fields[1].Description);
        Assert.Empty(FlowParams.Read("version: 1\nsteps: []\n")!);
        Assert.Null(FlowParams.Read(null));
        Assert.Null(FlowParams.Read("steps: [\n"));
    }

    [AvaloniaFact]
    public async Task The_flow_is_picked_from_the_projects_flows_and_its_parameters_are_fields()
    {
        var (_, tab, _) = await OpenAsync();

        var form = SelectCall(tab);

        var flow = Assert.Single(form.Parameters);
        Assert.Equal("flow", flow.Name);
        Assert.True(flow.IsChoice);
        Assert.Equal([Checkout, Login], flow.Choices);
        Assert.Equal(Checkout, flow.Value);
        Assert.True(form.HasFlowParameters);
        Assert.Equal(["email", "express"], form.FlowParameters.Select(f => f.Name));
        Assert.Equal("guest@example.com", form.FlowParameters[0].Value);
        Assert.Equal(["true", "false"], form.FlowParameters[1].Choices);

        form.FlowParameters[1].Value = "true";

        Assert.Contains("  - call:\n      flow: flows/checkout.flow.yaml\n      with: { email: guest@example.com, express: true }\n", tab.Document.Text, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public async Task Another_flow_keeps_only_its_own_parameters()
    {
        var (_, tab, _) = await OpenAsync();
        var form = SelectCall(tab);

        form.Parameters.Single(f => f.Name == "flow").Value = Login;

        // Written in the long form, the step stays long; the old flow's email is gone.
        Assert.EndsWith("  - call:\n      flow: flows/log-in.flow.yaml\n", tab.Document.Text, StringComparison.Ordinal);
        var again = tab.Steps!.Form!;
        Assert.Equal(["user", "attempts"], again.FlowParameters.Select(f => f.Name));
        again.FlowParameters[0].Value = "alice";
        Assert.Contains("  - call:\n      flow: flows/log-in.flow.yaml\n      with: { user: alice }\n", tab.Document.Text, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public async Task A_flow_that_cannot_be_read_leaves_with_as_yaml()
    {
        var (_, tab, _) = await OpenAsync(Text.Replace(Checkout, "flows/gone.flow.yaml", StringComparison.Ordinal));

        var form = SelectCall(tab);

        Assert.False(form.HasFlowParameters);
        Assert.Equal("flows/gone.flow.yaml cannot be read, so \"with\" is edited as YAML.", form.FlowNote);
        Assert.Contains("flows/gone.flow.yaml", form.Parameters.Single(f => f.Name == "flow").Choices);
        Assert.True(form.Parameters.Single(f => f.Name == "with").IsYaml);
    }

    [AvaloniaFact]
    public async Task The_call_form_renders_in_the_details_panel()
    {
        var (shell, tab, _) = await OpenAsync();
        var window = new MainWindow { DataContext = shell, Width = 1280, Height = 800 };
        window.Show();
        SelectCall(tab);
        Render(window, "33-call-step");

        Assert.Equal(2, window.GetVisualDescendants().OfType<ItemsControl>().First(c => c.Name == "FlowParameters").ItemCount);
    }

    private static void Render(Window window, string? snapshot = null)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        if (snapshot is not null && Environment.GetEnvironmentVariable("DESKTOP_SCREENSHOTS") is { Length: > 0 } folder)
        {
            Directory.CreateDirectory(folder);
            window.CaptureRenderedFrame()?.Save(Path.Combine(folder, snapshot + ".png"), new PngBitmapEncoderOptions());
        }
    }
}
