using System.IO;
using System.Text.Json;

namespace Blink;

/// <summary>
/// User-configurable settings, persisted as JSON under %AppData%\Blink\settings.json.
/// </summary>
public sealed class AppSettings
{
    /// <summary>Minutes between rest breaks.</summary>
    public double IntervalMinutes { get; set; } = 30;

    /// <summary>How long each rest break lasts, in seconds.</summary>
    public double BreakSeconds { get; set; } = 60;

    /// <summary>Whether the user can dismiss a break early (Esc / click).</summary>
    public bool AllowSkip { get; set; } = true;

    /// <summary>Start scheduling automatically when the app launches.</summary>
    public bool StartEnabled { get; set; } = true;

    /// <summary>
    /// UI language as a culture code ("en", "fr", "es"); empty means follow the OS.
    /// </summary>
    public string Language { get; set; } = "";

    private static string SettingsPath
    {
        get
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Blink");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, "settings.json");
        }
    }

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                var loaded = JsonSerializer.Deserialize<AppSettings>(json);
                if (loaded is not null)
                    return loaded.Sanitized();
            }
        }
        catch
        {
            // Corrupt or unreadable file: fall back to defaults.
        }

        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(Sanitized(),
                new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsPath, json);
        }
        catch
        {
            // Non-fatal: settings just won't persist.
        }
    }

    private AppSettings Sanitized()
    {
        // Clamp to sane ranges so a bad file can't wedge the app.
        IntervalMinutes = Math.Clamp(IntervalMinutes, 1, 24 * 60);
        BreakSeconds = Math.Clamp(BreakSeconds, 5, 3600);
        return this;
    }
}
