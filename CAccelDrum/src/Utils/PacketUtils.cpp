#include <Arduino.h>
#include <string_view>
#include "Utils/PacketUtils.h"

namespace PacketUtils
{
    std::string getBytesHex(SerialPacket& packet, uint32_t groupSize, uint32_t groupsPerLine)
    {
        std::stringstream ss;
        byte *ptr = reinterpret_cast<byte *>(&packet);
        for (int i = 0; i < sizeof(SerialPacket); i++)
        {
            ss << std::uppercase << std::setfill('0') << std::setw(2) << std::hex << (uint32_t)ptr[i];
            if ((i + 1) % groupSize == 0 && (i + 1) % (groupSize * groupsPerLine) != 0)
                ss << ' ';
            if ((i + 1) % (groupSize * groupsPerLine) == 0 && i < sizeof(SerialPacket) - 1)
                ss << '\n';
        }
        return ss.str();
    }

    void printToPackets(std::string_view str)
    {
        size_t len = str.length();
        for (uint32_t chunkIndex = 0; chunkIndex < len; chunkIndex += TextPacket::sizeStr)
        {
            std::string_view slice = str.substr(chunkIndex, TextPacket::sizeStr);
            TextPacket p{
                .length = slice.length(),
                .hasNext = slice.size() == TextPacket::sizeStr && len > TextPacket::sizeStr};
            slice.copy(p.string.data(), slice.size());
            serial.send(PacketType::Text, &p, sizeof(p));
        }
    }

    void printlnToPackets(std::string_view str)
    {
        size_t len = str.length();
        for (uint32_t chunkIndex = 0; chunkIndex < len; chunkIndex += TextPacket::sizeStr)
        {
            std::string_view slice = str.substr(chunkIndex, TextPacket::sizeStr);
            TextPacket p{
                .length = slice.length(),
                .hasNext = true};
            slice.copy(p.string.data(), slice.size());
            serial.send(PacketType::Text, &p, sizeof(p));
        }
        TextPacket p{
            .length = 1,};
        p.string[0] = '\n';
        serial.send(PacketType::Text, &p, sizeof(p));
    }
}