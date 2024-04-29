#include <Arduino.h>
#include <string>

namespace Utils
{
    int formatMetric(char *buf, size_t size, uint32_t num, uint32_t decimalPlaces = 1)
    {
        if (num >= 1'000'000)
            return snprintf(buf, size, "%.*fm", decimalPlaces, num / 1'000'000.0f);
        else if (num >= 1'000)
            return snprintf(buf, size, "%.*fk", decimalPlaces, num / 1'000.0f);
        return snprintf(buf, size, "%u", num);
    }
}