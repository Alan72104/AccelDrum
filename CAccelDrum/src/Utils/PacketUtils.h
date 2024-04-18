#pragma once
#include <concepts>
#include <sstream>
#include <iomanip>
#include "Serial/SerialManager.h"
#include "Serial/SerialPackets.h"
#include "Utils/Utils.h"    

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

    std::string getBytesHex(SerialPacket& packet, uint32_t groupSize = 8, uint32_t groupsPerLine = 4);

    inline void sendConfigureAck(ConfigurePacket::Type type, ConfigurePacket::Val ack)
    {
        PacketUtils::send(PacketType::Configure, ConfigurePacket
        {
            type,
            ack
        });
    }

    void printToPackets(std::string_view str);

    void printlnToPackets(std::string_view str);

    template <typename... Args>
    void printfToPackets(const char *format, Args... args)
    {
        std::string s = Utils::stringSprintf(format, args...);
        printToPackets(s);
    }

    template <typename... Args>
    void printlnfToPackets(const char *format, Args... args)
    {
        std::string s = Utils::stringSprintf(format, args...);
        printlnToPackets(s);
    }
}