using System.Windows;

namespace Blink;

/// <summary>Hidden diagnostic window that shows the running <see cref="Log"/> feed.</summary>
public partial class LogWindow : Window
{
    private static LogWindow? _instance;

    private LogWindow()
    {
        InitializeComponent();

        foreach (var entry in Log.Entries)
            LogList.Items.Add(entry);
        ScrollToEnd();

        Log.EntryAdded += Log_EntryAdded;
        Closed += (_, _) =>
        {
            Log.EntryAdded -= Log_EntryAdded;
            _instance = null;
        };
    }

    /// <summary>Opens the log window, or brings the existing one to the front.</summary>
    public static void ShowOrActivate()
    {
        if (_instance is null)
        {
            _instance = new LogWindow();
            _instance.Show();
        }
        else
        {
            _instance.Activate();
        }
    }

    private void Log_EntryAdded(object? sender, string entry)
    {
        LogList.Items.Add(entry);
        ScrollToEnd();
    }

    private void ScrollToEnd()
    {
        if (LogList.Items.Count > 0)
            LogList.ScrollIntoView(LogList.Items[^1]);
    }
}
