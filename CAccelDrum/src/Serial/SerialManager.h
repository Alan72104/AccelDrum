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

    void send(PacketType type, void* packet, size_t size);

    void sendNative(SerialPacket& packet);

    bool tryDequeueInbound(SerialPacket& outPacket);

    int getPacketCount() const;

    int getCorruptedPacketCount() const;

private:
    CircularBuffer<byte, sizeof(SerialPacket) * 2> parsingQueue;
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