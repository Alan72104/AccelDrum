#pragma once
#include <Arduino.h>
#include <array>
#include "Utils/Utils.h"

enum class PacketType : uint32_t
{
    None,
    Accel,
    Text,
    Count
};

struct SerialPacket
{
    static constexpr size_t sizeExpected = 64;
    static constexpr size_t sizeInner = 48;
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

struct TextPacket
{
    static constexpr size_t sizeStr = SerialPacket::sizeInner - sizeof(uint32_t);
    uint32_t length;
    std::array<char, sizeStr> string;
} __attribute__((packed));