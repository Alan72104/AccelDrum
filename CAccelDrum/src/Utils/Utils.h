#pragma once
#include <Arduino.h>
#include <concepts>
#include <string>
#include <string_view>

namespace Utils
{
    template <std::integral T>
    constexpr T reverseBytewise(T value)
    {
        constexpr size_t size = sizeof(T) * 8;
        T result = 0;
        for (size_t i = 0; i < size; i += 8)
            result |= (value & ((T)0xFF << i)) >> i << (size - 8 - i);
        return result;
    }

    template <typename... Args>
    std::string stringSprintf(const char *format, Args... args)
    {
        int length = std::snprintf(nullptr, 0, format, args...);
        assert(length >= 0);

        char *buf = new char[length + 1];
        std::snprintf(buf, length + 1, format, args...);

        std::string str(buf);
        delete[] buf;
        return str;
    }

    void printToPackets(std::string_view str);
}