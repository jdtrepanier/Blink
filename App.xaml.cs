using System.Globalization;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;
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
    private WinForms.ToolStripMenuItem _skipNextItem = null!;
    private WinForms.ToolStripMenuItem _restItem = null!;
    private WinForms.ToolStripMenuItem _settingsItem = null!;
    private WinForms.ToolStripMenuItem _exitItem = null!;

    // Counts down to the next break.
    private readonly DispatcherTimer _scheduleTimer = new();
    private readonly BreakScheduler _scheduler = new();

    // Drives the countdown while a break is on screen.
    private readonly DispatcherTimer _breakTimer = new();
    private DateTime _breakEndsAt;
    private readonly List<OverlayWindow> _overlays = new();

    // The "break starting soon" popup; null whenever no warning is currently shown.
    private BreakWarningWindow? _warningWindow;

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
        ApplySchedulerSettings();

        BuildTray();

        _scheduleTimer.Interval = TimeSpan.FromSeconds(1);
        _scheduleTimer.Tick += OnScheduleTimerTick;

        _breakTimer.Interval = TimeSpan.FromMilliseconds(250);
        _breakTimer.Tick += OnBreakTimerTick;

        SystemEvents.SessionSwitch += OnSystemEventsSessionSwitch;
        SystemEvents.PowerModeChanged += OnSystemEventsPowerModeChanged;

        SetEnabled(_settings.StartEnabled);
    }

    private void BuildTray()
    {
        var menu = new WinForms.ContextMenuStrip();

        _activeItem = new WinForms.ToolStripMenuItem("", null, (_, _) => ToggleActive());
        _skipNextItem = new WinForms.ToolStripMenuItem("", null, (_, _) => SkipNextBreak());
        _restItem = new WinForms.ToolStripMenuItem("", null, (_, _) => StartBreak());
        _settingsItem = new WinForms.ToolStripMenuItem("", null, (_, _) => ShowSettings());
        _exitItem = new WinForms.ToolStripMenuItem("", null, (_, _) => ExitApp());

        menu.Items.Add(_restItem);
        menu.Items.Add(new WinForms.ToolStripSeparator());
        menu.Items.Add(_activeItem);
        menu.Items.Add(_skipNextItem);
        menu.Items.Add(_settingsItem);
        menu.Items.Add(new WinForms.ToolStripSeparator());
        menu.Items.Add(_exitItem);

        // Skipping only makes sense while the countdown is actually running.
        menu.Opening += (_, _) => _skipNextItem.Enabled = _scheduler.Enabled && !_scheduler.BreakActive;

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
        _skipNextItem.Text = Strings.Tray_SkipNext;
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
        _activeItem.Checked = enabled;
        Log.Write(enabled ? "Activated" : "Deactivated");

        if (enabled)
        {
            _scheduler.Enable(DateTime.Now);
            _scheduleTimer.Start();
        }
        else
        {
            _scheduler.Disable();
            _scheduleTimer.Stop();
            CloseWarningWindow();
        }

        UpdateTrayIcon();
        UpdateTooltip();
    }

    private void ToggleActive() => SetEnabled(!_scheduler.Enabled);

    /// <summary>Pushes the next break back by a full interval, leaving the schedule otherwise untouched.</summary>
    private void SkipNextBreak()
    {
        if (!_scheduler.Enabled || _scheduler.BreakActive)
            return;

        _scheduler.ScheduleNextBreak(DateTime.Now);
        CloseWarningWindow();
        Log.Write("Next break skipped");
        UpdateTooltip();
    }

    /// <summary>Applies the interval/idle-reset settings to the scheduler.</summary>
    private void ApplySchedulerSettings()
    {
        _scheduler.Interval = TimeSpan.FromMinutes(_settings.IntervalMinutes);
        _scheduler.IdleResetThreshold = TimeSpan.FromMinutes(_settings.IdleResetMinutes);
    }

    private void OnScheduleTimerTick(object? sender, EventArgs e)
    {
        UpdateTooltip();

        var result = _scheduler.Tick(DateTime.Now, IdleTime.GetIdleTime());

        if (result.IdleResetTriggered)
            Log.Write($"Idle for {result.IdleDuration:mm\\:ss}: countdown reset");

        if (result.ShouldShowWarning && _settings.WarnBeforeBreak)
            ShowBreakWarning();

        if (result.ShouldStartBreak)
            StartBreak();
    }

    private void OnSystemEventsSessionSwitch(object? sender, SessionSwitchEventArgs e)
    {
        // SystemEvents raises this on its own worker thread; marshal to the UI thread
        // before touching DispatcherTimer/UI state.
        Dispatcher.Invoke(() =>
        {
            switch (e.Reason)
            {
                case SessionSwitchReason.SessionLock:
                    OnSessionLocked();
                    break;
                case SessionSwitchReason.SessionUnlock:
                    OnSessionUnlocked();
                    break;
            }
        });
    }

    private void OnSystemEventsPowerModeChanged(object? sender, PowerModeChangedEventArgs e)
    {
        // Sleep/hibernate doesn't always raise SessionLock first, so the countdown must be
        // paused here too - otherwise the wall-clock target is missed during suspend and a
        // break fires the instant the machine wakes. OnSessionLocked/Unlocked already guard
        // against being called twice (e.g. once for the real lock, once for suspend).
        Dispatcher.Invoke(() =>
        {
            switch (e.Mode)
            {
                case PowerModes.Suspend:
                    OnSessionLocked();
                    break;
                case PowerModes.Resume:
                    OnSessionUnlocked();
                    break;
            }
        });
    }

    private void OnSessionLocked()
    {
        var result = _scheduler.Lock(DateTime.Now);
        if (!result.Locked)
            return;

        Log.Write($"Locked: paused with {result.Remaining:mm\\:ss} remaining");
        _scheduleTimer.Stop();
    }

    private void OnSessionUnlocked()
    {
        var result = _scheduler.Unlock(DateTime.Now);

        switch (result.Action)
        {
            case BreakScheduler.UnlockAction.Ignored:
                return;
            case BreakScheduler.UnlockAction.Reset:
                Log.Write($"Unlocked after {result.LockedDuration:mm\\:ss}: countdown reset");
                break;
            case BreakScheduler.UnlockAction.Resumed:
                Log.Write($"Unlocked after {result.LockedDuration:mm\\:ss}: resumed with {result.Remaining:mm\\:ss} remaining");
                break;
        }

        _scheduleTimer.Start();
    }

    private void ShowBreakWarning()
    {
        Log.Write("Break warning shown");

        CloseWarningWindow();

        var warning = new BreakWarningWindow(_settings.AllowSkip);
        warning.SkipRequested += (_, _) => SkipNextBreak();
        _warningWindow = warning;
        warning.Show();
    }

    private void CloseWarningWindow()
    {
        _warningWindow?.Close();
        _warningWindow = null;
    }

    private void StartBreak()
    {
        if (_scheduler.BreakActive)
            return;

        Log.Write("Break started");
        _scheduler.BreakStarted();
        _breakEndsAt = DateTime.Now.AddSeconds(_settings.BreakSeconds);

        CloseWarningWindow();

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

    private void OnBreakTimerTick(object? sender, EventArgs e)
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
        if (!_scheduler.BreakActive)
            return;

        Log.Write("Break ended");
        _breakTimer.Stop();

        foreach (var overlay in _overlays)
            overlay.Close();
        _overlays.Clear();

        _scheduler.BreakEnded(DateTime.Now);

        UpdateTooltip();
    }

    private void ShowSettings()
    {
        var previousLanguage = _settings.Language;

        var dialog = new SettingsWindow(_settings);
        if (dialog.ShowDialog() == true)
        {
            _settings.Save();
            ApplySchedulerSettings();

            // Re-arm the schedule with the new interval (StartEnabled only affects launch).
            if (_scheduler.Enabled && !_scheduler.BreakActive)
                _scheduler.ScheduleNextBreak(DateTime.Now);

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
        _tray.Icon = IconFactory.CreateSleepyEye(paused: !_scheduler.Enabled);
        old?.Dispose();
    }

    private void UpdateTooltip()
    {
        string text;
        if (_scheduler.BreakActive)
        {
            text = Strings.Tooltip_Resting;
        }
        else if (_scheduler.Enabled)
        {
            var remaining = _scheduler.TimeUntilNextBreak(DateTime.Now);
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
        CloseWarningWindow();
        _scheduleTimer.Stop();
        _tray.Visible = false;
        _tray.Icon?.Dispose();
        _tray.Dispose();
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // SystemEvents is process-wide static state; unhook so this instance isn't kept alive.
        SystemEvents.SessionSwitch -= OnSystemEventsSessionSwitch;
        SystemEvents.PowerModeChanged -= OnSystemEventsPowerModeChanged;

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
