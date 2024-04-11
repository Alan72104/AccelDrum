#pragma once
#include <Arduino.h>
#include <limits>

class Timer
{
public:
    typedef std::function<uint64_t()> TimeFunction;

    Timer(uint64_t threshold = 0, bool running = false, TimeFunction timeFunction = ::millis) :
        threshold(threshold), running(running), timeFunction(timeFunction)
    {
    }

    bool isRunning()
    {
        return running;
    }

    Timer& start()
    {
        running = true;
        startTime = timeFunction();
        return *this;
    }

    Timer& restart()
    {
        return start();
    }

    Timer& stop()
    {
        running = false;
        return *this;
    }

    uint64_t elapsedTime()
    {
        return running ? timeFunction() - startTime : 0;
    }

    bool isElapsed()
    {
        return running ? timeFunction() - startTime >= threshold : false;
    }

    bool checkAndResetIfElapsed()
    {
        bool elapsed = isElapsed();
        if (elapsed)
            restart();
        return elapsed;
    }
private:
    uint64_t startTime = 0;
    uint64_t threshold = 0;
    TimeFunction timeFunction;
    bool running = false;
};