// Code-behind of the workspace view: view plumbing only. It sizes the
// project and bottom tracks from the remembered layout, collapses the bottom
// panel, reports the sizes a person drags the splitters to, and closes the New
// menu once one of its buttons is used.

using System.ComponentModel;
using Avalonia.Controls;
using Desktop.App.ViewModels;

namespace Desktop.App.Views;

/// <summary>An open project.</summary>
public partial class WorkspaceView : UserControl
{
    private const int ProjectColumn = 0;
    private const int BottomSplitterRow = 1;
    private const int BottomRow = 2;

    private readonly GridLength _splitter;
    private LayoutViewModel? _layout;

    /// <summary>Creates the view.</summary>
    public WorkspaceView()
    {
        InitializeComponent();
        _splitter = Regions.RowDefinitions[BottomSplitterRow].Height;
        foreach (var splitter in new[] { ProjectSplitter, BottomSplitter })
        {
            splitter.DragCompleted += (_, _) => ReportSizes();
        }

        NewMenu.AddHandler(Button.ClickEvent, (_, _) => NewMenuButton.Flyout?.Hide());
    }

    /// <inheritdoc />
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_layout is not null)
        {
            _layout.PropertyChanged -= OnLayoutChanged;
        }

        _layout = (DataContext as WorkspaceViewModel)?.Layout;
        if (_layout is not null)
        {
            _layout.PropertyChanged += OnLayoutChanged;
        }

        ApplyLayout();
    }

    private void OnLayoutChanged(object? sender, PropertyChangedEventArgs e) => ApplyLayout();

    /// <summary>Gives the tracks the remembered sizes; the collapsed bottom panel keeps only its header strip.</summary>
    private void ApplyLayout()
    {
        if (_layout is not { } layout)
        {
            return;
        }

        var columns = Regions.ColumnDefinitions;
        var rows = Regions.RowDefinitions;
        columns[ProjectColumn].Width = new GridLength(layout.ProjectWidth);
        rows[BottomSplitterRow].Height = layout.IsBottomOpen ? _splitter : new GridLength(0);
        rows[BottomRow].Height = layout.IsBottomOpen ? new GridLength(layout.BottomHeight) : GridLength.Auto;
    }

    private void ReportSizes()
    {
        _layout?.Resized(Regions.ColumnDefinitions[ProjectColumn].ActualWidth, Regions.RowDefinitions[BottomRow].ActualHeight);
    }
}
