#pragma once
#include <Arduino.h>

namespace Utils
{
    template <std::integral T>
    constexpr T reverseBytewise(T value)
    {
        constexpr std::size_t size = sizeof(T) * 8;
        T result = 0;
        for (std::size_t i = 0; i < size; i += 8)
            result |= (value & ((T)0xFF << i)) >> i << (size - 8 - i);
        return result;
    }
}