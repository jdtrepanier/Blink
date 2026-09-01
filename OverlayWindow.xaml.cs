using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
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

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

    private const uint SwpNoZOrder = 0x0004;
    private const uint SwpNoActivate = 0x0010;

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        // At this point the HWND still lives on whatever monitor it was created on, so this
        // window's own DPI transform doesn't yet match the target screen. SetWindowPos takes
        // physical pixels directly, sidestepping WPF's DIP conversion, so the window lands and
        // sizes correctly on the target monitor even when it has a different DPI scale.
        var hwnd = ((HwndSource)PresentationSource.FromVisual(this)!).Handle;
        var bounds = _screen.Bounds;
        SetWindowPos(hwnd, IntPtr.Zero, bounds.Left, bounds.Top, bounds.Width, bounds.Height, SwpNoZOrder | SwpNoActivate);

        Activate();
        Focus();
    }
}
