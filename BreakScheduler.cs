namespace Blink;

/// <summary>
/// Pure state machine for the break countdown: when the next break is due, how lock/sleep
/// pauses and resumes it, and when idle time resets it. Takes time as a parameter instead of
/// reading the clock or OS idle time itself, so it can be driven deterministically from tests.
/// </summary>
public sealed class BreakScheduler
{
    public TimeSpan Interval { get; set; }
    public TimeSpan IdleResetThreshold { get; set; }

    public bool Enabled { get; private set; }
    public bool BreakActive { get; private set; }
    public DateTime NextBreakAt { get; private set; }

    private DateTime? _lockedAt;
    private TimeSpan _remainingAtLock;

    // Only reset once per idle stretch; requires activity to bring the user back before it can fire again.
    private bool _idleResetArmed = true;

    public void Enable(DateTime now)
    {
        Enabled = true;
        ScheduleNextBreak(now);
    }

    public void Disable() => Enabled = false;

    public void ScheduleNextBreak(DateTime now) => NextBreakAt = now + Interval;

    public TimeSpan TimeUntilNextBreak(DateTime now)
    {
        var remaining = NextBreakAt - now;
        return remaining < TimeSpan.Zero ? TimeSpan.Zero : remaining;
    }

    public readonly record struct TickResult(bool ShouldStartBreak, bool IdleResetTriggered, TimeSpan IdleDuration);

    /// <summary>Call once per schedule tick. <paramref name="idle"/> is how long since the last user input.</summary>
    public TickResult Tick(DateTime now, TimeSpan idle)
    {
        if (!Enabled || BreakActive)
            return new TickResult(false, false, idle);

        var idleReset = false;
        if (idle >= IdleResetThreshold)
        {
            if (_idleResetArmed)
            {
                _idleResetArmed = false;
                ScheduleNextBreak(now);
                idleReset = true;
            }
        }
        else
        {
            _idleResetArmed = true;
        }

        return new TickResult(now >= NextBreakAt, idleReset, idle);
    }

    public readonly record struct LockResult(bool Locked, TimeSpan Remaining);

    /// <summary>Call when the session locks or the machine suspends.</summary>
    public LockResult Lock(DateTime now)
    {
        if (!Enabled || BreakActive || _lockedAt is not null)
            return new LockResult(false, TimeSpan.Zero);

        _lockedAt = now;
        _remainingAtLock = TimeUntilNextBreak(now);
        return new LockResult(true, _remainingAtLock);
    }

    public enum UnlockAction { Ignored, Reset, Resumed }

    public readonly record struct UnlockResult(UnlockAction Action, TimeSpan LockedDuration, TimeSpan Remaining);

    /// <summary>Call when the session unlocks or the machine resumes.</summary>
    public UnlockResult Unlock(DateTime now)
    {
        if (_lockedAt is null)
            return new UnlockResult(UnlockAction.Ignored, TimeSpan.Zero, TimeSpan.Zero);

        var lockedDuration = now - _lockedAt.Value;
        _lockedAt = null;

        // Being away long enough to lock counts as the activity that re-arms idle detection.
        _idleResetArmed = true;

        if (!Enabled || BreakActive)
            return new UnlockResult(UnlockAction.Ignored, lockedDuration, TimeSpan.Zero);

        // A lock long enough to count as idle resets the countdown; a brief lock just resumes it.
        if (lockedDuration >= IdleResetThreshold)
        {
            ScheduleNextBreak(now);
            return new UnlockResult(UnlockAction.Reset, lockedDuration, TimeSpan.Zero);
        }

        NextBreakAt = now + _remainingAtLock;
        return new UnlockResult(UnlockAction.Resumed, lockedDuration, _remainingAtLock);
    }

    public void BreakStarted() => BreakActive = true;

    public void BreakEnded(DateTime now)
    {
        BreakActive = false;
        if (Enabled)
            ScheduleNextBreak(now);
    }
}
