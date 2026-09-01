using System.Globalization;
using System.Resources;

namespace Blink;

/// <summary>
/// Strongly-typed access to the localized strings embedded from Resources\Strings*.resx.
/// Hand-written (rather than the VS ResXFileCodeGenerator) so it builds from the CLI too.
/// Values are resolved against <see cref="CultureInfo.CurrentUICulture"/> at access time.
/// </summary>
public static class Strings
{
    private static readonly ResourceManager Rm =
        new("Blink.Resources.Strings", typeof(Strings).Assembly);

    private static string Get(string key) =>
        Rm.GetString(key, CultureInfo.CurrentUICulture) ?? key;

    /// <summary>Formats a resource string with the current culture.</summary>
    public static string Format(string key, params object[] args) =>
        string.Format(CultureInfo.CurrentCulture, Get(key), args);

    public static string Tray_Pause => Get(nameof(Tray_Pause));
    public static string Tray_Resume => Get(nameof(Tray_Resume));
    public static string Tray_RestNow => Get(nameof(Tray_RestNow));
    public static string Tray_Settings => Get(nameof(Tray_Settings));
    public static string Tray_Exit => Get(nameof(Tray_Exit));

    public static string Tooltip_Resting => Get(nameof(Tooltip_Resting));
    public static string Tooltip_Paused => Get(nameof(Tooltip_Paused));

    public static string Overlay_RestYourEyes => Get(nameof(Overlay_RestYourEyes));
    public static string Overlay_Tip => Get(nameof(Overlay_Tip));
    public static string Overlay_SkipHint => Get(nameof(Overlay_SkipHint));

    public static string Settings_Title => Get(nameof(Settings_Title));
    public static string Settings_BreakEvery => Get(nameof(Settings_BreakEvery));
    public static string Settings_BreakLength => Get(nameof(Settings_BreakLength));
    public static string Settings_AllowSkip => Get(nameof(Settings_AllowSkip));
    public static string Settings_StartScheduling => Get(nameof(Settings_StartScheduling));
    public static string Settings_StartWithWindows => Get(nameof(Settings_StartWithWindows));
    public static string Settings_Language => Get(nameof(Settings_Language));
    public static string Settings_OK => Get(nameof(Settings_OK));
    public static string Settings_Cancel => Get(nameof(Settings_Cancel));

    public static string Language_System => Get(nameof(Language_System));
    public static string Language_English => Get(nameof(Language_English));
    public static string Language_French => Get(nameof(Language_French));
    public static string Language_Spanish => Get(nameof(Language_Spanish));

    // Formatted messages.
    public static string Tooltip_NextBreak(int minutes, int seconds) =>
        Format(nameof(Tooltip_NextBreak), minutes, seconds);

    public static string Settings_Validation_Interval(double min, double max) =>
        Format(nameof(Settings_Validation_Interval), min, max);

    public static string Settings_Validation_Break(double min, double max) =>
        Format(nameof(Settings_Validation_Break), min, max);
}
