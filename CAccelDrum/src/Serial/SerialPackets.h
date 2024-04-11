#pragma once
#include <Arduino.h>
#include "../Utils/Utils.h"

enum class PacketType : uint32_t
{
    None,
    Accel,
    Count,
};

struct SerialPacket
{
    static constexpr uint32_t sizeExpected = 64;
    static constexpr uint32_t sizeInner = 48;
    static constexpr uint64_t magicExpected = 0xDEADBEEF80085069;
    static constexpr uint64_t magicExpectedReversed = Utils::reverseBytewise(magicExpected);

    PacketType type;
    struct Inner
    {
        byte data[sizeInner];
    } inner;    
    uint32_t crc32;
    uint64_t magic;
} __attribute__((packed));

static_assert(sizeof(SerialPacket) == SerialPacket::sizeExpected, "Packet size was modified");

template <typename T>
struct SerialPacketTyped
{
    PacketType type;
    T inner;
    uint32_t crc32;
    uint64_t magic;
} __attribute__((packed));

struct AccelPacket
{
    uint64_t deltaMicros;
    float ax, ay, az;
    float gx, gy, gz, gw;
    float ex, ey, ez;
} __attribute__((packed));