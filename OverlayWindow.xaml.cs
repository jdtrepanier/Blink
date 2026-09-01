using System.Windows;
using System.Windows.Input;
using WinForms = System.Windows.Forms;

namespace Blink;

/// <summary>
/// A blank full-screen overlay pinned to a single monitor for the duration of a rest break.
/// </summary>
public partial class OverlayWindow : Window
{
    private readonly WinForms.Screen _screen;

    /// <summary>Raised when the user asks to skip the break (only if skipping is allowed).</summary>
    public event EventHandler? SkipRequested;

    public OverlayWindow(WinForms.Screen screen, bool allowSkip)
    {
        InitializeComponent();
        _screen = screen;
        SkipHint.Visibility = allowSkip ? Visibility.Visible : Visibility.Collapsed;

        SourceInitialized += OnSourceInitialized;

        if (allowSkip)
        {
            // Only Escape skips the break: a stray click shouldn't dismiss it.
            KeyDown += (_, e) =>
            {
                if (e.Key == Key.Escape)
                    SkipRequested?.Invoke(this, EventArgs.Empty);
            };
        }
    }

    public void SetCountdown(TimeSpan remaining)
    {
        var seconds = (int)Math.Ceiling(remaining.TotalSeconds);
        CountdownText.Text = seconds >= 60
            ? $"{seconds / 60}:{seconds % 60:00}"
            : seconds.ToString();
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        // Screen bounds are in physical pixels; WPF positions in DIPs.
        // Convert using this window's own device transform so mixed-DPI setups line up.
        var source = PresentationSource.FromVisual(this);
        var transform = source?.CompositionTarget?.TransformFromDevice ?? System.Windows.Media.Matrix.Identity;

        var bounds = _screen.Bounds;
        var topLeft = transform.Transform(new System.Windows.Point(bounds.Left, bounds.Top));
        var size = transform.Transform(new Vector(bounds.Width, bounds.Height));

        Left = topLeft.X;
        Top = topLeft.Y;
        Width = size.X;
        Height = size.Y;

        Activate();
        Focus();
    }
}
