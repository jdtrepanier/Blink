using System.Runtime.InteropServices;

namespace Blink;

/// <summary>Reads how long the system has gone without keyboard/mouse input.</summary>
internal static class IdleTime
{
    [StructLayout(LayoutKind.Sequential)]
    private struct LASTINPUTINFO
    {
        public uint cbSize;
        public uint dwTime;
    }

    [DllImport("user32.dll")]
    private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

    public static TimeSpan GetIdleTime()
    {
        var info = new LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf<LASTINPUTINFO>() };
        if (!GetLastInputInfo(ref info))
            return TimeSpan.Zero;

        // Both values are GetTickCount()-style uint millisecond counters, so the
        // subtraction wraps correctly even across the ~49.7-day rollover.
        var idleMs = unchecked((uint)Environment.TickCount - info.dwTime);
        return TimeSpan.FromMilliseconds(idleMs);
    }
}
