using Blink;

namespace Blink.Tests;

public class BreakSchedulerTests
{
    private static readonly DateTime Start = new(2026, 1, 1, 9, 0, 0);

    private static BreakScheduler CreateEnabled(TimeSpan interval, TimeSpan idleResetThreshold)
    {
        var scheduler = new BreakScheduler
        {
            Interval = interval,
            IdleResetThreshold = idleResetThreshold,
        };
        scheduler.Enable(Start);
        return scheduler;
    }

    [Fact]
    public void Tick_BeforeIntervalElapses_DoesNotStartBreak()
    {
        var scheduler = CreateEnabled(TimeSpan.FromMinutes(30), TimeSpan.FromMinutes(5));

        var result = scheduler.Tick(Start + TimeSpan.FromMinutes(29), TimeSpan.Zero);

        Assert.False(result.ShouldStartBreak);
    }

    [Fact]
    public void Tick_AfterIntervalElapses_StartsBreak()
    {
        var scheduler = CreateEnabled(TimeSpan.FromMinutes(30), TimeSpan.FromMinutes(5));

        var result = scheduler.Tick(Start + TimeSpan.FromMinutes(30), TimeSpan.Zero);

        Assert.True(result.ShouldStartBreak);
    }

    [Fact]
    public void Tick_WhileDisabled_NeverStartsBreak()
    {
        var scheduler = new BreakScheduler
        {
            Interval = TimeSpan.FromMinutes(30),
            IdleResetThreshold = TimeSpan.FromMinutes(5),
        };

        var result = scheduler.Tick(Start + TimeSpan.FromHours(1), TimeSpan.Zero);

        Assert.False(result.ShouldStartBreak);
    }

    [Fact]
    public void Tick_IdlePastThreshold_ResetsCountdownOnce()
    {
        var scheduler = CreateEnabled(TimeSpan.FromMinutes(30), TimeSpan.FromMinutes(5));
        var now = Start + TimeSpan.FromMinutes(10);

        var first = scheduler.Tick(now, TimeSpan.FromMinutes(5));
        Assert.True(first.IdleResetTriggered);
        Assert.Equal(now + TimeSpan.FromMinutes(30), scheduler.NextBreakAt);

        // Still idle a second later: must not keep re-arming the countdown.
        var second = scheduler.Tick(now + TimeSpan.FromSeconds(1), TimeSpan.FromMinutes(5) + TimeSpan.FromSeconds(1));
        Assert.False(second.IdleResetTriggered);
    }

    [Fact]
    public void Lock_RecordsRemainingTime_AndPreventsDoubleLock()
    {
        var scheduler = CreateEnabled(TimeSpan.FromMinutes(30), TimeSpan.FromMinutes(5));
        var lockTime = Start + TimeSpan.FromMinutes(20);

        var first = scheduler.Lock(lockTime);
        Assert.True(first.Locked);
        Assert.Equal(TimeSpan.FromMinutes(10), first.Remaining);

        var second = scheduler.Lock(lockTime + TimeSpan.FromMinutes(1));
        Assert.False(second.Locked);
    }

    [Fact]
    public void Unlock_ShortLock_ResumesWithRemainingTime()
    {
        var scheduler = CreateEnabled(TimeSpan.FromMinutes(30), TimeSpan.FromMinutes(5));
        var lockTime = Start + TimeSpan.FromMinutes(20);
        scheduler.Lock(lockTime); // 10 minutes remaining

        var unlockTime = lockTime + TimeSpan.FromMinutes(2); // shorter than the 5-minute idle threshold
        var result = scheduler.Unlock(unlockTime);

        Assert.Equal(BreakScheduler.UnlockAction.Resumed, result.Action);
        Assert.Equal(unlockTime + TimeSpan.FromMinutes(10), scheduler.NextBreakAt);
    }

    [Fact]
    public void Unlock_LongLock_ResetsCountdownInsteadOfResuming()
    {
        var scheduler = CreateEnabled(TimeSpan.FromMinutes(30), TimeSpan.FromMinutes(5));
        var lockTime = Start + TimeSpan.FromMinutes(20);
        scheduler.Lock(lockTime); // 10 minutes remaining

        var unlockTime = lockTime + TimeSpan.FromMinutes(10); // longer than the 5-minute idle threshold
        var result = scheduler.Unlock(unlockTime);

        Assert.Equal(BreakScheduler.UnlockAction.Reset, result.Action);
        Assert.Equal(unlockTime + TimeSpan.FromMinutes(30), scheduler.NextBreakAt);
    }

    [Fact]
    public void Suspend_OvernightSleep_DoesNotFireBreakImmediatelyOnResume()
    {
        // Regression test: the schedule used to be driven purely by DispatcherTimer ticks tied
        // to SessionLock/Unlock. If the machine suspends without an explicit lock (common for
        // sleep-on-idle), the wall-clock target was missed during sleep and a break fired the
        // instant the machine woke up. The scheduler must treat suspend/resume as a pause too.
        var scheduler = CreateEnabled(TimeSpan.FromMinutes(30), TimeSpan.FromMinutes(5));
        var suspendTime = Start + TimeSpan.FromMinutes(10); // 20 minutes still remaining

        scheduler.Lock(suspendTime); // App wires PowerModes.Suspend to Lock()

        var resumeTime = suspendTime + TimeSpan.FromHours(8); // overnight sleep
        var unlockResult = scheduler.Unlock(resumeTime); // App wires PowerModes.Resume to Unlock()

        Assert.Equal(BreakScheduler.UnlockAction.Reset, unlockResult.Action);

        // The very next tick right after waking must not report a break as due.
        var tick = scheduler.Tick(resumeTime, TimeSpan.Zero);
        Assert.False(tick.ShouldStartBreak);
    }

    [Fact]
    public void Unlock_WithoutPriorLock_IsIgnored()
    {
        var scheduler = CreateEnabled(TimeSpan.FromMinutes(30), TimeSpan.FromMinutes(5));

        var result = scheduler.Unlock(Start + TimeSpan.FromMinutes(5));

        Assert.Equal(BreakScheduler.UnlockAction.Ignored, result.Action);
    }

    [Fact]
    public void Lock_WhileBreakActive_IsIgnored()
    {
        var scheduler = CreateEnabled(TimeSpan.FromMinutes(30), TimeSpan.FromMinutes(5));
        scheduler.Tick(Start + TimeSpan.FromMinutes(30), TimeSpan.Zero);
        scheduler.BreakStarted();

        var result = scheduler.Lock(Start + TimeSpan.FromMinutes(31));

        Assert.False(result.Locked);
    }

    [Fact]
    public void BreakEnded_WhileEnabled_ReschedulesFromNow()
    {
        var scheduler = CreateEnabled(TimeSpan.FromMinutes(30), TimeSpan.FromMinutes(5));
        scheduler.BreakStarted();

        var endTime = Start + TimeSpan.FromMinutes(30) + TimeSpan.FromSeconds(45);
        scheduler.BreakEnded(endTime);

        Assert.False(scheduler.BreakActive);
        Assert.Equal(endTime + TimeSpan.FromMinutes(30), scheduler.NextBreakAt);
    }

    [Fact]
    public void BreakEnded_WhileDisabled_DoesNotReschedule()
    {
        var scheduler = new BreakScheduler { Interval = TimeSpan.FromMinutes(30) };
        scheduler.BreakStarted();

        scheduler.BreakEnded(Start);

        Assert.False(scheduler.BreakActive);
        Assert.False(scheduler.Enabled);
    }
}
