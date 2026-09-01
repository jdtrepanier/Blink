using System.Globalization;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using WinForms = System.Windows.Forms;

namespace Blink;

public partial class App : System.Windows.Application
{
    // Per-user, per-session guard so Blink only runs once at a time.
    private const string SingleInstanceMutexName = "Local\\Blink.SingleInstance.9F3C1A2E";
    private Mutex? _singleInstanceMutex;

    private AppSettings _settings = null!;
    private WinForms.NotifyIcon _tray = null!;
    private WinForms.ToolStripMenuItem _activeItem = null!;
    private WinForms.ToolStripMenuItem _restItem = null!;
    private WinForms.ToolStripMenuItem _settingsItem = null!;
    private WinForms.ToolStripMenuItem _exitItem = null!;

    // Counts down to the next break.
    private readonly DispatcherTimer _scheduleTimer = new();
    private DateTime _nextBreakAt;

    // Drives the countdown while a break is on screen.
    private readonly DispatcherTimer _breakTimer = new();
    private DateTime _breakEndsAt;
    private readonly List<OverlayWindow> _overlays = new();

    private bool _enabled;
    private bool _breakActive;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Dev utility: regenerate the app icon from the same artwork the tray uses.
        //   Blink.exe --export-icon app.ico
        if (e.Args.Contains("--export-icon"))
        {
            var idx = Array.IndexOf(e.Args, "--export-icon");
            var path = idx + 1 < e.Args.Length ? e.Args[idx + 1] : "app.ico";
            IconFactory.SaveIco(path);
            Shutdown();
            return;
        }

        // Allow only one running instance; if another already holds the mutex, bow out.
        _singleInstanceMutex = new Mutex(initiallyOwned: true, SingleInstanceMutexName, out var createdNew);
        if (!createdNew)
        {
            _singleInstanceMutex.Dispose();
            _singleInstanceMutex = null;
            Shutdown();
            return;
        }

        _settings = AppSettings.Load();
        ApplyCulture(_settings.Language);

        BuildTray();

        _scheduleTimer.Interval = TimeSpan.FromSeconds(1);
        _scheduleTimer.Tick += ScheduleTimer_Tick;

        _breakTimer.Interval = TimeSpan.FromMilliseconds(250);
        _breakTimer.Tick += BreakTimer_Tick;

        SetEnabled(_settings.StartEnabled);
    }

    private void BuildTray()
    {
        var menu = new WinForms.ContextMenuStrip();

        _activeItem = new WinForms.ToolStripMenuItem("", null, (_, _) => ToggleActive());
        _restItem = new WinForms.ToolStripMenuItem("", null, (_, _) => StartBreak());
        _settingsItem = new WinForms.ToolStripMenuItem("", null, (_, _) => ShowSettings());
        _exitItem = new WinForms.ToolStripMenuItem("", null, (_, _) => ExitApp());

        menu.Items.Add(_restItem);
        menu.Items.Add(new WinForms.ToolStripSeparator());
        menu.Items.Add(_activeItem);
        menu.Items.Add(_settingsItem);
        menu.Items.Add(new WinForms.ToolStripSeparator());
        menu.Items.Add(_exitItem);

        _tray = new WinForms.NotifyIcon
        {
            Icon = IconFactory.CreateSleepyEye(),
            Visible = true,
            Text = "Blink",
            ContextMenuStrip = menu,
        };

        // Double-click starts a break immediately.
        _tray.DoubleClick += (_, _) => StartBreak();

        RefreshTrayTexts();
    }

    /// <summary>(Re)applies localized text to the tray menu items and tooltip.</summary>
    private void RefreshTrayTexts()
    {
        _activeItem.Text = Strings.Tray_Active;
        _restItem.Text = Strings.Tray_RestNow;
        _settingsItem.Text = Strings.Tray_Settings;
        _exitItem.Text = Strings.Tray_Exit;
        UpdateTooltip();
    }

    /// <summary>
    /// Sets the current thread culture from a culture code; empty follows the OS.
    /// </summary>
    private static void ApplyCulture(string code)
    {
        CultureInfo culture;
        try
        {
            culture = string.IsNullOrWhiteSpace(code)
                ? CultureInfo.InstalledUICulture
                : CultureInfo.GetCultureInfo(code);
        }
        catch (CultureNotFoundException)
        {
            culture = CultureInfo.InstalledUICulture;
        }

        Thread.CurrentThread.CurrentCulture = culture;
        Thread.CurrentThread.CurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
    }

    private void SetEnabled(bool enabled)
    {
        _enabled = enabled;
        _activeItem.Checked = enabled;

        if (enabled)
        {
            ScheduleNextBreak();
            _scheduleTimer.Start();
        }
        else
        {
            _scheduleTimer.Stop();
        }

        UpdateTrayIcon();
        UpdateTooltip();
    }

    private void ToggleActive() => SetEnabled(!_enabled);

    private void ScheduleNextBreak()
    {
        _nextBreakAt = DateTime.Now.AddMinutes(_settings.IntervalMinutes);
    }

    private void ScheduleTimer_Tick(object? sender, EventArgs e)
    {
        UpdateTooltip();

        if (!_breakActive && DateTime.Now >= _nextBreakAt)
            StartBreak();
    }

    private void StartBreak()
    {
        if (_breakActive)
            return;

        _breakActive = true;
        _breakEndsAt = DateTime.Now.AddSeconds(_settings.BreakSeconds);

        foreach (var screen in WinForms.Screen.AllScreens)
        {
            var overlay = new OverlayWindow(screen, _settings.AllowSkip);
            overlay.SkipRequested += (_, _) => EndBreak();
            _overlays.Add(overlay);
            overlay.Show();
        }

        UpdateBreakCountdown();
        _breakTimer.Start();
    }

    private void BreakTimer_Tick(object? sender, EventArgs e)
    {
        if (DateTime.Now >= _breakEndsAt)
        {
            EndBreak();
            return;
        }

        UpdateBreakCountdown();
    }

    private void UpdateBreakCountdown()
    {
        var remaining = _breakEndsAt - DateTime.Now;
        if (remaining < TimeSpan.Zero)
            remaining = TimeSpan.Zero;

        foreach (var overlay in _overlays)
            overlay.SetCountdown(remaining);
    }

    private void EndBreak()
    {
        if (!_breakActive)
            return;

        _breakTimer.Stop();

        foreach (var overlay in _overlays)
            overlay.Close();
        _overlays.Clear();

        _breakActive = false;

        if (_enabled)
            ScheduleNextBreak();

        UpdateTooltip();
    }

    private void ShowSettings()
    {
        var previousLanguage = _settings.Language;

        var dialog = new SettingsWindow(_settings);
        if (dialog.ShowDialog() == true)
        {
            _settings.Save();

            // Re-arm the schedule with the new interval (StartEnabled only affects launch).
            if (_enabled && !_breakActive)
                ScheduleNextBreak();

            if (_settings.Language != previousLanguage)
                ApplyCulture(_settings.Language);

            // Re-localize the tray (also refreshes the tooltip). Any windows opened
            // afterward pick up the new language automatically.
            RefreshTrayTexts();
        }
    }

    private void UpdateTrayIcon()
    {
        var old = _tray.Icon;
        _tray.Icon = IconFactory.CreateSleepyEye(paused: !_enabled);
        old?.Dispose();
    }

    private void UpdateTooltip()
    {
        string text;
        if (_breakActive)
        {
            text = Strings.Tooltip_Resting;
        }
        else if (_enabled)
        {
            var remaining = _nextBreakAt - DateTime.Now;
            if (remaining < TimeSpan.Zero)
                remaining = TimeSpan.Zero;
            text = Strings.Tooltip_NextBreak((int)remaining.TotalMinutes, remaining.Seconds);
        }
        else
        {
            text = Strings.Tooltip_Paused;
        }

        // NotifyIcon.Text is capped at 63 characters.
        _tray.Text = text.Length > 63 ? text[..63] : text;
    }

    private void ExitApp()
    {
        EndBreak();
        _scheduleTimer.Stop();
        _tray.Visible = false;
        _tray.Icon?.Dispose();
        _tray.Dispose();
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // Release the single-instance guard so the next launch can start.
        if (_singleInstanceMutex is not null)
        {
            _singleInstanceMutex.ReleaseMutex();
            _singleInstanceMutex.Dispose();
            _singleInstanceMutex = null;
        }

        base.OnExit(e);
    }
}
