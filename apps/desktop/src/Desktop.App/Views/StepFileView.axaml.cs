// Code-behind of the step file view: view plumbing only. It draws the problem
// lines the view model reports, shows their messages on hover, scrolls to the
// line the view model reveals, and binds Save to Ctrl+S (Cmd+S on macOS).

using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using AvaloniaEdit.Rendering;
using Desktop.App.ViewModels;

namespace Desktop.App.Views;

/// <summary>A step file in a tab.</summary>
public partial class StepFileView : UserControl
{
    private readonly ProblemLineRenderer _renderer = new();
    private StepFileViewModel? _viewModel;

    /// <summary>Creates the view.</summary>
    public StepFileView()
    {
        InitializeComponent();
        Editor.TextArea.TextView.BackgroundRenderers.Add(_renderer);
        Editor.TextArea.TextView.PointerMoved += (_, e) => ShowMessagesAt(e.GetPosition(Editor.TextArea.TextView));
    }

    /// <summary>The renderer that marks problem lines; tests read which lines it marks.</summary>
    internal ProblemLineRenderer Marks => _renderer;

    /// <inheritdoc />
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_viewModel is not null)
        {
            _viewModel.DiagnosticsChanged -= OnDiagnosticsChanged;
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _viewModel = DataContext as StepFileViewModel;
        KeyBindings.Clear();
        if (_viewModel is not null)
        {
            _viewModel.DiagnosticsChanged += OnDiagnosticsChanged;
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            // Ctrl+S, or Cmd+S on macOS: the platform's command key.
            var modifiers = Application.Current?.PlatformSettings?.HotkeyConfiguration.CommandModifiers ?? KeyModifiers.Control;
            KeyBindings.Add(new KeyBinding { Gesture = new KeyGesture(Key.S, modifiers), Command = _viewModel.SaveCommand });
        }

        OnDiagnosticsChanged(this, EventArgs.Empty);
        RevealPending();
    }

    private void OnDiagnosticsChanged(object? sender, EventArgs e)
    {
        _renderer.Marks = _viewModel?.LineMarks ?? new Dictionary<int, LineMark>();
        Editor.TextArea.TextView.InvalidateLayer(_renderer.Layer);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(StepFileViewModel.RevealedLine))
        {
            RevealPending();
        }
    }

    private void RevealPending()
    {
        if (_viewModel?.RevealedLine is not { } line || line > Editor.Document?.LineCount)
        {
            return;
        }

        Editor.TextArea.Caret.Line = line;
        Editor.TextArea.Caret.Column = 1;
        Editor.ScrollToLine(line);
    }

    private void ShowMessagesAt(Point point)
    {
        var position = Editor.TextArea.TextView.GetPosition(point + Editor.TextArea.TextView.ScrollOffset);
        var messages = position is { } p ? _viewModel?.MessagesAt(p.Line) : null;
        ToolTip.SetTip(Editor, messages);
        ToolTip.SetIsOpen(Editor, messages is not null);
    }

    /// <summary>Tints the lines that have problems: red for errors, amber for warnings.</summary>
    internal sealed class ProblemLineRenderer : IBackgroundRenderer
    {
        private static readonly IBrush ErrorBrush = new SolidColorBrush(Color.FromArgb(0x40, 0xD1, 0x34, 0x38));
        private static readonly IBrush WarningBrush = new SolidColorBrush(Color.FromArgb(0x40, 0xC7, 0x8A, 0x00));

        /// <summary>The marked lines.</summary>
        public IReadOnlyDictionary<int, LineMark> Marks { get; set; } = new Dictionary<int, LineMark>();

        /// <inheritdoc />
        public KnownLayer Layer => KnownLayer.Background;

        /// <inheritdoc />
        public void Draw(TextView textView, DrawingContext drawingContext)
        {
            if (!textView.VisualLinesValid || Marks.Count == 0)
            {
                return;
            }

            foreach (var visualLine in textView.VisualLines)
            {
                if (!Marks.TryGetValue(visualLine.FirstDocumentLine.LineNumber, out var mark) || mark == LineMark.None)
                {
                    continue;
                }

                var top = visualLine.VisualTop - textView.ScrollOffset.Y;
                drawingContext.FillRectangle(
                    mark == LineMark.Error ? ErrorBrush : WarningBrush,
                    new Rect(0, top, textView.Bounds.Width + textView.ScrollOffset.X, visualLine.Height));
            }
        }
    }
}
