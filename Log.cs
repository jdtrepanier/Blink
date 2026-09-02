namespace Blink;

/// <summary>
/// In-memory ring buffer of timestamped diagnostic entries, surfaced by the hidden LogWindow.
/// All writes happen on the UI thread (DispatcherTimer callbacks, or SystemEvents already
/// marshaled there), so no locking is needed.
/// </summary>
internal static class Log
{
    private const int MaxEntries = 500;
    private static readonly List<string> _entries = new();

    public static event EventHandler<string>? EntryAdded;

    public static IReadOnlyList<string> Entries => _entries;

    public static void Write(string message)
    {
        var line = $"{DateTime.Now:HH:mm:ss} {message}";

        _entries.Add(line);
        if (_entries.Count > MaxEntries)
            _entries.RemoveAt(0);

        EntryAdded?.Invoke(null, line);
    }
}
