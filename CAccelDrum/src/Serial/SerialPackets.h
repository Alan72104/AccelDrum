#pragma once
#include <Arduino.h>
#include <array>
#include "Utils/BitUtils.h"

enum class PacketType : uint32_t
{
    None,
    Accel,
    RawAccel,
    Text,
    Configure,
    Count
};

struct SerialPacket
{
    static constexpr size_t sizeExpected = 144;
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

struct RawAccelPacket
{
    struct Pack
    {
        uint32_t deltaMicros;
        float ax, ay, az;
        float gx, gy, gz;
    } __attribute__((packed));
    static constexpr uint32_t packCount = 4;
    std::array<Pack, packCount> packs;
    byte padding[sizeof(SerialPacket::Inner) - sizeof(packs)];
} __attribute__((packed));

struct TextPacket
{
    static constexpr size_t sizeStr = SerialPacket::sizeInner - sizeof(uint32_t) - sizeof(bool);
    uint32_t length;
    bool hasNext;
    std::array<char, sizeStr> string;
} __attribute__((packed));

struct ConfigurePacket
{
    enum class Type : uint32_t
    {
        None,
        PollForData,
        Backlight,
        ResetDmp,
        Count
    };
    enum class Val : int32_t
    {
        None = 0,
        BacklightGet,
        BacklightResultOn,
        BacklightResultOff,
        BacklightAck,
        BacklightSetOn,
        BacklightSetOff,
        BacklightSetToggle,
        ResetDmpAck
    };
    struct Settings
    {
        uint8_t accelRange;
        uint8_t gyroRange;
        uint8_t accel;
    } __attribute__((packed));
    static constexpr size_t sizeData = SerialPacket::sizeInner - sizeof(Type) - sizeof(Val);
    Type type;
    Val value;
    std::array<byte, sizeData> data;
} __attribute__((packed));