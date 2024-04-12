#include <Arduino.h>
#include <string_view>
#include "Serial/SerialManager.h"
#include "Serial/SerialPackets.h"

namespace Utils
{
    void printToPackets(std::string_view str)
    {
        size_t len = str.length();
        for (uint32_t chunkIndex = 0; chunkIndex < len; chunkIndex += TextPacket::sizeStr)
        {
            std::string_view slice = str.substr(chunkIndex, TextPacket::sizeStr);
            TextPacket p{
                .length = slice.length()};
            slice.copy(p.string.data(), slice.size());
            // serial.send(PacketType::Text, &p, sizeof(p));
            serial.send(PacketType::Text, p);
        }
    }
}