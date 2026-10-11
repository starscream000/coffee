// Code-behind of the workspace view: view plumbing only. It sizes the
// project, details and bottom tracks from the remembered layout, collapses the
// details and bottom panels, and reports the sizes a person drags the
// splitters to.

using System.ComponentModel;
using Avalonia.Controls;
using Desktop.App.ViewModels;

namespace Desktop.App.Views;

/// <summary>An open project.</summary>
public partial class WorkspaceView : UserControl
{
    private const int ProjectColumn = 0;
    private const int DetailsSplitterColumn = 3;
    private const int DetailsColumn = 4;
    private const int BottomSplitterRow = 1;
    private const int BottomRow = 2;

    private readonly GridLength _splitter;
    private LayoutViewModel? _layout;

    /// <summary>Creates the view.</summary>
    public WorkspaceView()
    {
        InitializeComponent();
        _splitter = Regions.ColumnDefinitions[1].Width;
        foreach (var splitter in new[] { ProjectSplitter, DetailsSplitter, BottomSplitter })
        {
            splitter.DragCompleted += (_, _) => ReportSizes();
        }

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

    /// <summary>Gives the tracks the remembered sizes; a collapsed panel keeps only its header strip (bottom) or nothing (details).</summary>
    private void ApplyLayout()
    {
        if (_layout is not { } layout)
        {
            return;
        }

        var columns = Regions.ColumnDefinitions;
        var rows = Regions.RowDefinitions;
        columns[ProjectColumn].Width = new GridLength(layout.ProjectWidth);
        columns[DetailsSplitterColumn].Width = layout.IsDetailsOpen ? _splitter : new GridLength(0);
        columns[DetailsColumn].Width = layout.IsDetailsOpen ? new GridLength(layout.DetailsWidth) : new GridLength(0);
        rows[BottomSplitterRow].Height = layout.IsBottomOpen ? _splitter : new GridLength(0);
        rows[BottomRow].Height = layout.IsBottomOpen ? new GridLength(layout.BottomHeight) : GridLength.Auto;
    }

    private void ReportSizes()
    {
        var columns = Regions.ColumnDefinitions;
        _layout?.Resized(columns[ProjectColumn].ActualWidth, columns[DetailsColumn].ActualWidth, Regions.RowDefinitions[BottomRow].ActualHeight);
    }
}
