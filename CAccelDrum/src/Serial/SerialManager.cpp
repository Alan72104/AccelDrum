#include <Arduino.h>
#include "SerialManager.h"
#include "SerialPackets.h"
#include "Display/Display.h"

SerialManager serial;

SerialManager::SerialManager() : parsingQueue(),
                                 inboundQueue(),
                                 crcOut(),
                                 crcIn(),
                                 lastLong(0),
                                 packetCount(0),
                                 corruptedPacketCount(0),
                                 inboundQueueMutex()
{
}

void SerialManager::init()
{
    Serial.begin(1'000'000);
    while (!Serial)
        ;
    while (Serial.available() && Serial.read())
        ;
}

void SerialManager::run()
{
    scheduler.schedule(this);
    receive();
}

void SerialManager::send(PacketType type, void* packet, size_t size)
{
    if (size != sizeof(SerialPacket::Inner))
        return;
    SerialPacket::Inner &inner = *reinterpret_cast<SerialPacket::Inner *>(packet);
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

void SerialManager::sendNative(SerialPacket &packet)
{
    Serial.write(reinterpret_cast<byte *>(&packet), sizeof(packet));
}

void SerialManager::receive()
{
    const int maxBytesPerBatch = sizeof(SerialPacket) * 4;
    int bytesRead = 0;
    while (Serial.available() && bytesRead < maxBytesPerBatch)
    {
        bytesRead++;
        int _int = Serial.read();
        if (_int == -1)
            return;
        byte b = _int;
        parsingQueue.push(b);
        lastLong <<= 8;
        lastLong |= b;
        if (parsingQueue.size() > sizeof(SerialPacket))
            parsingQueue.shift(); // pop() pops from the END of the buffer, which is what we just added
        if (parsingQueue.size() == sizeof(SerialPacket) &&
            lastLong == SerialPacket::magicExpectedReversed)
        {
            SerialPacket packet;
            for (int i = 0; i < sizeof(SerialPacket); i++)
                reinterpret_cast<byte *>(&packet)[i] = parsingQueue.shift();
            tryEnqueueInbound(packet);
        }
    }
}

bool SerialManager::tryEnqueueInbound(SerialPacket &packet)
{
    crcIn.restart();
    crcIn.add(reinterpret_cast<uint8_t *>(&packet.type), sizeof(packet.type));
    crcIn.add(reinterpret_cast<uint8_t *>(&packet.inner), sizeof(packet.inner));
    uint32_t crc = crcIn.calc();
    if (crc != packet.crc32)
    {
        corruptedPacketCount++;
        return false;
    }
    inboundQueue.push(packet);
    packetCount++;
    return true;
}

bool SerialManager::tryDequeueInbound(SerialPacket &outPacket)
{
    if (inboundQueue.size() > 0)
    {
        outPacket = inboundQueue.shift();
        return true;
    }
    return false;
}

uint32_t SerialManager::getCorruptedPacketCount() const
{
    return corruptedPacketCount;
}

uint32_t SerialManager::getPacketCount() const
{
    return packetCount;
}