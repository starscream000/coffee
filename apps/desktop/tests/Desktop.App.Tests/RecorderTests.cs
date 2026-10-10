// The recorder's place (instruction D0003, task 20): Record opens one tab that
// says recording arrives with a later engine version, and records nothing.

using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Desktop.App.Services;
using Desktop.App.Tests.Fakes;
using Desktop.App.ViewModels;
using Desktop.App.Views;

namespace Desktop.App.Tests;

public sealed class RecorderTests
{
    private const string Root = "/work/shop-tests";

    [AvaloniaFact]
    public async Task Record_opens_one_tab_that_says_recording_is_not_here_yet()
    {
        var engine = new FakeEngineService();
        var files = new FakeProjectFiles();
        var shell = new ShellViewModel(engine, new MemorySettingsStore(), new FakeFolderPicker(Root), files, new ImmediateDispatcher(), p => p == Root);
        var window = new MainWindow { DataContext = shell, Width = 1280, Height = 800 };
        window.Show();
        await shell.OpenProjectAsync(Root);
        var workspace = shell.Workspace!;

        workspace.RecordCommand.Execute(null);
        workspace.SelectedTab = workspace.Actions;
        workspace.RecordCommand.Execute(null);

        var tab = Assert.IsType<RecorderTabViewModel>(workspace.SelectedTab);
        Assert.Single(workspace.Tabs.OfType<RecorderTabViewModel>());
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        var heading = window.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Name == "RecorderHeading");
        Assert.Equal("Recording is not here yet", heading.Text);
        Assert.Contains("cannot record", RecorderTabViewModel.Now, StringComparison.Ordinal);
        Assert.Empty(files.Writes);

        tab.CloseCommand.Execute(null);
        Assert.DoesNotContain(tab, workspace.Tabs);
    }
}
