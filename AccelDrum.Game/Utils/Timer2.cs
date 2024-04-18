using System;
using System.Diagnostics;
using Printer = System.Action<System.TimeSpan>;

namespace AccelDrum.Game.Utils;

public class Timer2 : Stopwatch, IDisposable
{
    public long ThresholdMs { get; set; }
    private Printer? printer;

    public Timer2(long thresholdMs)
    {
        ThresholdMs = thresholdMs;
        Start();
    }

    public Timer2(long thresholdMs, Printer printer) : this(thresholdMs)
    {
        this.printer = printer;
    }

    public Timer2(Printer printer) : this(0, printer)
    {
    }

    public bool CheckAndResetIfElapsed()
    {
        if (!IsRunning)
        {
            Restart();
            return false;
        }

        if (ElapsedMilliseconds >= ThresholdMs)
        {
            Restart();
            return true;
        }
        return false;
    }

    public new Timer2 Stop()
    {
        base.Stop();
        return this;
    }

    public new Timer2 Start()
    {
        base.Start();
        return this;
    }

    public void Dispose()
    {
        if (IsRunning)
        {
            printer?.Invoke(Elapsed);
            Stop();
        }
    }
}
