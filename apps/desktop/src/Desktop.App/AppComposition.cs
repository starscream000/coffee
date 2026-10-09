// Wires the app's objects together in one place (ADR D0005: no container).

using Avalonia.Controls;
using Desktop.App.Services;
using Desktop.App.ViewModels;

namespace Desktop.App;

/// <summary>Creates the app's object graph.</summary>
public static class AppComposition
{
    /// <summary>Creates the main window's view model with the real services.</summary>
    /// <param name="owner">The window that owns dialogs.</param>
    /// <returns>The view model.</returns>
    public static ShellViewModel CreateShell(Func<TopLevel?> owner)
    {
        var dispatcher = new AvaloniaDispatcher();
        var settings = new JsonSettingsStore(JsonSettingsStore.DefaultPath);
        var engine = new EngineService(settings, dispatcher);
        return new ShellViewModel(
            engine,
            settings,
            new AvaloniaFolderPicker(owner),
            new DiskProjectFiles(),
            dispatcher,
            dialogs: new AvaloniaDialogService(() => owner() as Window),
            delay: new RealDelay());
    }
}
