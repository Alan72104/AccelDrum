#pragma once
#include <Arduino.h>
#include <string_view>
#include <Serial/SerialManager.h>
#include <Serial/SerialPackets.h>

namespace Utils
{
    template <typename... Args>
    std::string stringSprintf(const char *format, Args... args)
    {
        int length = snprintf(nullptr, 0, format, args...);

        char *buf = new char[length + 1];
        snprintf(buf, length + 1, format, args...);

        std::string str(buf);
        delete[] buf;
        return str;
    }

    int formatMetric(char *buf, size_t size, uint32_t num, uint32_t decimalPlaces = 1);
}