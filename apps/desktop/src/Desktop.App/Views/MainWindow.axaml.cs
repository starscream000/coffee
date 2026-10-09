// Code-behind of the main window.

using Avalonia.Controls;
using Desktop.Protocol;

namespace Desktop.App.Views;

/// <summary>The main window of the app.</summary>
public partial class MainWindow : Window
{
    /// <summary>Creates the window and its controls.</summary>
    public MainWindow()
    {
        InitializeComponent();
        Title = Product.DisplayName;
        Placeholder.Text = Product.DisplayName;
    }
}
