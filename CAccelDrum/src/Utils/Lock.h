#include <Arduino.h>

class Lock;

struct ScopedLocker
{
public:
    ~ScopedLocker()
    {
        xSemaphoreGive(handle);
    }

private:
    SemaphoreHandle_t handle;

    ScopedLocker(SemaphoreHandle_t handle) : handle(handle)
    {
        xSemaphoreTake(handle, portMAX_DELAY);
    }

    friend Lock;
};

class Lock
{
public:
    Lock() : handle(xSemaphoreCreateMutex())
    {
    }

    ScopedLocker lock()
    {
        return ScopedLocker(handle);
    }

private:
    SemaphoreHandle_t handle;
};