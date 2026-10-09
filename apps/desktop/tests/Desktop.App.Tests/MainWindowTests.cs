// Smoke test of the main window on the headless platform.

using Avalonia.Headless.XUnit;
using Desktop.App.Views;
using Desktop.Protocol;

namespace Desktop.App.Tests;

public sealed class MainWindowTests
{
    [AvaloniaFact]
    public void Title_is_the_product_display_name()
    {
        var window = new MainWindow();
        window.Show();
        Assert.Equal(Product.DisplayName, window.Title);
    }
}
