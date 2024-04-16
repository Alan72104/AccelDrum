#pragma once
#include <concepts>

namespace BitUtils
{
    template <std::integral T>
    inline constexpr T reverseBytewise(T value)
    {
        const size_t size = sizeof(T) * 8;
        T result = 0;
        for (size_t i = 0; i < size; i += 8)
            result |= (value & ((T)0xFF << i)) >> i << (size - 8 - i);
        return result;
    }
}