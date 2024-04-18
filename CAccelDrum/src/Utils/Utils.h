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
        int length = std::snprintf(nullptr, 0, format, args...);

        char *buf = new char[length + 1];
        std::snprintf(buf, length + 1, format, args...);

        std::string str(buf);
        delete[] buf;
        return str;
    }
}