using System.Diagnostics;

namespace AccelDrum.Game.Utils;

public class Timer2
{
    public long ThresholdMs { get; set; }
    public bool Running => stopwatch.IsRunning;
    private Stopwatch stopwatch;

    public Timer2(long thresholdMs)
    {
        stopwatch = new Stopwatch();
        ThresholdMs = thresholdMs;
    }

    public bool CheckAndResetIfElapsed()
    {
        if (!stopwatch.IsRunning)
        {
            stopwatch.Restart();
            return false;
        }

        if (stopwatch.ElapsedMilliseconds >= ThresholdMs)
        {
            stopwatch.Restart();
            return true;
        }
        return false;
    }

    public Timer2 Stop()
    {
        stopwatch.Stop();
        return this;
    }

    public Timer2 Start()
    {
        stopwatch.Start();
        return this;
    }
}
