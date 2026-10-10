// The step list beside the text editor on Avalonia's headless platform. With
// DESKTOP_SCREENSHOTS set to a folder, the test also saves what it rendered.

using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Desktop.App.Services;
using Desktop.App.Tests.Fakes;
using Desktop.App.ViewModels;
using Desktop.App.Views;

namespace Desktop.App.Tests.StepFiles;

public sealed class StepListViewTests
{
    private const string Root = "/work/shop-tests";

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

    private static T Find<T>(Window window, string name)
        where T : Control =>
        window.GetVisualDescendants().OfType<T>().FirstOrDefault(c => c.Name == name && c.IsEffectivelyVisible)
        ?? throw new InvalidOperationException($"No {typeof(T).Name} named {name} is shown.");

    [AvaloniaFact]
    public async Task The_step_list_shows_beside_the_text_and_text_only_files_say_why()
    {
        var engine = new FakeEngineService { Tests = [Make.Test("tests/a.test.yaml", "A")], Actions = StepFormTests.RealActions() };
        var files = new FakeProjectFiles();
        files.Files["tests/a.test.yaml"] = StepOutlineTests.Sample;
        files.Files["tests/b.test.yaml"] = "version: 1\nsteps: [back]\n";
        var shell = new ShellViewModel(engine, new MemorySettingsStore(), new FakeFolderPicker(Root), files, new ImmediateDispatcher(), p => p == Root);
        var window = new MainWindow { DataContext = shell, Width = 1280, Height = 800 };
        window.Show();
        await shell.OpenProjectAsync(Root);
        shell.Workspace!.OpenFile("tests/a.test.yaml");
        var tab = (StepFileViewModel)shell.Workspace.SelectedTab!;
        tab.Steps!.SelectCommand.Execute(tab.Steps.Sections[1].Steps[3]);
        Render(window, "20-step-list");

        Assert.Equal(3, Find<ItemsControl>(window, "StepSections").ItemCount);
        Assert.True(Find<Button>(window, "AddStepButton").IsEffectivelyEnabled);
        tab.Steps.StartAddingCommand.Execute(null);
        Render(window, "21-step-list-picker");
        Assert.True(Find<TextBox>(window, "PickerSearch").IsEffectivelyVisible);

        tab.Steps.CancelAddingCommand.Execute(null);
        Render(window, "22-step-form");
        Assert.Equal("click", Find<TextBlock>(window, "FormAction").Text);
        Assert.Equal(4, Find<ItemsControl>(window, "FormParameters").ItemCount);
        Assert.Equal(3, Find<ItemsControl>(window, "FormSettings").ItemCount);

        shell.Workspace.OpenFile("tests/b.test.yaml");
        Render(window);
        Assert.StartsWith("The step list cannot show this file.", Find<TextBlock>(window, "StepListProblem").Text, StringComparison.Ordinal);
    }
}
