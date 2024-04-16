#pragma once
#include <concepts>
#include "Serial/SerialManager.h"
#include "Serial/SerialPackets.h"

namespace PacketUtils
{
    template <typename T>
    requires (sizeof(T) == sizeof(SerialPacket::Inner))
    inline SerialPacketTyped<T> as(SerialPacket packet)
    {
        return *reinterpret_cast<SerialPacketTyped<T> *>(&packet);
    }

    template <typename T>
    requires (sizeof(T) == sizeof(SerialPacket::Inner))
    inline T innerAs(SerialPacket packet)
    {
        return (*reinterpret_cast<SerialPacketTyped<T> *>(&packet)).inner;
    }

    template <typename T>
    requires (sizeof(T) == sizeof(SerialPacket::Inner))
    inline void send(PacketType type, T& packet)
    {
        serial.send(type, &packet, sizeof(T));
    }

    template <typename T>
    requires (sizeof(T) == sizeof(SerialPacket::Inner))
    inline void send(PacketType type, T&& packet)
    {
        serial.send(type, &packet, sizeof(T));
    }

    inline void sendConfigureAck(ConfigurePacket::Type type)
    {
        PacketUtils::send(PacketType::Configure, ConfigurePacket
        {
            type,
            ConfigurePacket::Val::Ack
        });
    }
}