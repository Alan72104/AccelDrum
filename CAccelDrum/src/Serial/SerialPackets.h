#pragma once
#include <Arduino.h>
#include <array>
#include "Utils/BitUtils.h"

enum class PacketType : uint32_t
{
    None,
    Accel,
    Text,
    Configure,
    Count
};

struct SerialPacket
{
    static constexpr size_t sizeExpected = 128;
    static constexpr size_t sizeInner = sizeExpected - sizeof(PacketType) - sizeof(uint32_t) - sizeof(uint64_t);
    static constexpr uint64_t magicExpected = 0xDEADBEEF80085069;
    static constexpr uint64_t magicExpectedReversed = BitUtils::reverseBytewise(magicExpected);

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
    byte padding[SerialPacket::sizeInner - sizeof(uint64_t) - sizeof(float) * 10];
} __attribute__((packed));

struct TextPacket
{
    static constexpr size_t sizeStr = SerialPacket::sizeInner - sizeof(uint32_t);
    uint32_t length;
    std::array<char, sizeStr> string;
} __attribute__((packed));

struct ConfigurePacket
{
    enum class Type : uint32_t
    {
        None,
        Backlight,
        ResetDmp,
        Count
    };
    enum class Val : int32_t
    {
        None = 0,
        Get = -1000,
        Result = -2000,
        Ack = -100,
        Set = 1,
        BacklightGet = Get,
        BacklightResultOn = Result,
        BacklightResultOff = Result + 1,
        BacklightAck = Ack,
        BacklightSetOn = Set,
        BacklightSetOff = Set + 1,
        BacklightSetToggle = Set + 2,
        ResetDmpAck = Ack,
    };
    Type type;
    Val value;
    byte padding[SerialPacket::sizeInner - sizeof(uint32_t) * 2];
} __attribute__((packed));