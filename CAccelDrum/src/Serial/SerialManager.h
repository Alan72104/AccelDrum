#pragma once
#include <Arduino.h>
#include <CRC32.h>
#define LIBCALL_DEEP_SLEEP_SCHEDULER
#include <DeepSleepScheduler.h>
#include <CircularBuffer.hpp>
#include <mutex>
#include "SerialPackets.h"

class SerialManager;
extern SerialManager serial;

class SerialManager : public Runnable
{
public:
    SerialManager();

    void init();

    virtual void run() override;

    template<typename T>
    requires (sizeof(T) == sizeof(SerialPacket::inner))
    void send(PacketType type, T& packet);

    // void send(PacketType type, void* innerGeneric, size_t innerSize);

    void sendNative(SerialPacket& packet);

    template<typename T>
    requires (sizeof(T) == sizeof(SerialPacket::inner))
    bool tryDequeueInbound(PacketType type, T& outPacket);

    int getPacketCount() const;

    int getCorruptedPacketCount() const;

private:
    CircularBuffer<byte, 128> parsingQueue;
    CircularBuffer<SerialPacket, 16> inboundQueue;
    CRC32 crcOut;
    CRC32 crcIn;
    uint64_t lastLong;
    int packetCount;
    int corruptedPacketCount;
    std::mutex inboundQueueMutex;

    void receive();
    bool tryEnqueueInbound(SerialPacket& packet);
};

template<typename T>
requires (sizeof(T) == sizeof(SerialPacket::inner))
void SerialManager::send(PacketType type, T& packet)
// void SerialManager::send(PacketType type, void *innerGeneric, size_t innerSize)
{
    // SerialPacket::Inner &inner = *reinterpret_cast<SerialPacket::Inner *>(innerGeneric);
    SerialPacket::Inner &inner = *reinterpret_cast<SerialPacket::Inner *>(&packet);
    if (!(type > PacketType::None && type < PacketType::Count))
        return;
    SerialPacket outPacket{
        .type = type,
        .inner = inner,
        .magic = SerialPacket::magicExpected};
    crcOut.restart();
    crcOut.add(reinterpret_cast<uint8_t *>(&outPacket.type), sizeof(outPacket.type));
    crcOut.add(reinterpret_cast<uint8_t *>(&outPacket.inner), sizeof(outPacket.inner));
    outPacket.crc32 = crcOut.calc();
    sendNative(outPacket);
}