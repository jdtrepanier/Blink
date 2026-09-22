using System.Windows;
using System.Windows.Input;

namespace Blink;

/// <summary>
/// Small topmost toast shown near the tray before a break starts, replacing the native
/// balloon so a "Skip" action can be offered directly. The caller owns its lifetime:
/// this window never closes itself, so it can be shown or torn down alongside the rest
/// of the break-scheduling state in <see cref="App"/>.
/// </summary>
public partial class BreakWarningWindow : Window
{
    /// <summary>Raised when the user asks to skip the upcoming break (only if skipping is allowed).</summary>
    public event EventHandler? SkipRequested;

    public BreakWarningWindow(bool allowSkip)
    {
        InitializeComponent();
        SkipButton.Visibility = allowSkip ? Visibility.Visible : Visibility.Collapsed;

        // Rendered at 64px and downscaled to the 28px slot, so it stays crisp on high-DPI screens.
        AppIcon.Source = IconFactory.CreateSleepyEyeImageSource(64);

        Loaded += (_, _) => PositionNearTray();
    }

    private void PositionNearTray()
    {
        const double margin = 16;
        var workArea = SystemParameters.WorkArea;
        Left = workArea.Right - Width - margin;
        Top = workArea.Bottom - Height - margin;
    }

    private void OnSkipClick(object sender, RoutedEventArgs e) => SkipRequested?.Invoke(this, EventArgs.Empty);

    // The Skip button marks its own MouseLeftButtonDown as handled, so this only fires for
    // clicks elsewhere on the popup - a plain dismiss, leaving the break schedule untouched.
    private void OnBackgroundClick(object sender, MouseButtonEventArgs e) => Close();
}
