// Turns the absolute path of an image the engine wrote (a step's screenshot)
// into a bitmap for an Image control; a file that cannot be read gives no image.

using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;

namespace Desktop.App.Views;

/// <summary>Reads an image file into a bitmap.</summary>
public sealed class ImageFileConverter : IValueConverter
{
    /// <summary>The shared instance, for <c>{x:Static}</c> in XAML.</summary>
    public static readonly ImageFileConverter Instance = new();

    /// <summary>Reads the image at a path.</summary>
    /// <param name="value">The absolute path, or null.</param>
    /// <param name="targetType">Unused.</param>
    /// <param name="parameter">Unused.</param>
    /// <param name="culture">Unused.</param>
    /// <returns>The bitmap, or null when there is no path or the file cannot be read.</returns>
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string path || !File.Exists(path))
        {
            return null;
        }

        try
        {
            using var stream = File.OpenRead(path);
            return new Bitmap(stream);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>Not supported.</summary>
    /// <param name="value">Unused.</param>
    /// <param name="targetType">Unused.</param>
    /// <param name="parameter">Unused.</param>
    /// <param name="culture">Unused.</param>
    /// <returns>Never returns.</returns>
    /// <exception cref="NotSupportedException">Always.</exception>
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
