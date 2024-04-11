#include <Arduino.h>
#include "SerialManager.h"
#include "SerialPackets.h"

SerialManager serial;

SerialManager::SerialManager() :
    parsingQueue(),
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
    Serial.begin(1152000);
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

// template<typename T>
// requires (sizeof(T) == sizeof(SerialPacket::inner))
// void SerialManager::send(PacketType type, T& packet)
void SerialManager::send(PacketType type, void* innerGeneric)
{
    SerialPacket::Inner& inner = *reinterpret_cast<SerialPacket::Inner*>(innerGeneric);
    if (!(type > PacketType::None && type < PacketType::Count))
        return;
    SerialPacket outPacket
    {
        .type = type,
        .inner = inner,
        .magic = SerialPacket::magicExpected
    };
    crcOut.restart();
    crcOut.add(reinterpret_cast<uint8_t*>(&outPacket.type), sizeof(outPacket.type));
    crcOut.add(reinterpret_cast<uint8_t*>(&outPacket.inner), sizeof(outPacket.inner));
    outPacket.crc32 = crcOut.calc();
    sendNative(outPacket);
}

void SerialManager::sendNative(SerialPacket& packet)
{
    Serial.write((byte*)&packet, sizeof(packet));
}

void SerialManager::receive()
{
    const int bytesPerCycle = 128;
    int bytesRead = 0;
    while (Serial.available() && bytesRead < bytesPerCycle)
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
            parsingQueue.pop();
        if (parsingQueue.size() == sizeof(SerialPacket) &&
            lastLong == SerialPacket::magicExpectedReversed)
        {
            SerialPacket packet;
            for (int i = 0; i < sizeof(SerialPacket); i++)
                reinterpret_cast<byte*>(&packet)[i] = parsingQueue.pop();
            tryEnqueueInbound(packet);
        }
    }
}

bool SerialManager::tryEnqueueInbound(SerialPacket& packet)
{
    crcIn.restart();
    crcIn.add(reinterpret_cast<uint8_t*>(&packet.type), sizeof(packet.type));
    crcIn.add(reinterpret_cast<uint8_t*>(&packet.inner), sizeof(packet.inner));
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

template<typename T>
requires (sizeof(T) == sizeof(SerialPacket::inner))
bool SerialManager::tryDequeueInbound(PacketType type, T& outPacket)
{
    if (inboundQueue.size() > 0)
    {
        SerialPacket inPacket = inboundQueue.first();
        PacketType inType = inPacket.type;
        if (inType > PacketType::None && inType < PacketType::Count &&
            inType == type)
        {
            outPacket = inboundQueue.pop();
            return true;
        }
    }
    return false;
}

int SerialManager::getCorruptedPacketCount() const
{
    return corruptedPacketCount;
}

int SerialManager::getPacketCount() const
{
    return packetCount;
}