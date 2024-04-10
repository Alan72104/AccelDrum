#pragma once
#include <Arduino.h>
#include <Bounce2.h>
#include <MPU6050_6Axis_MotionApps20.h>
#include <CRC32.h>

constexpr uint64_t oneSecMillis = 1000;
constexpr uint8_t buttonPin1 = 36;
constexpr uint8_t buttonPin2 = 39;
constexpr uint8_t ledPinDebug = 13;
constexpr uint8_t debugLedBrightness = 128;
extern MPU6050 mpu;
extern Bounce2::Button btn1;
extern Bounce2::Button btn2;
extern VectorFloat accumulatedPos;
extern CRC32 crcOut;
extern CRC32 crcIn;

void updateBtns();
void processDmpPacket();
void receivePacket();
void blinkLed();

struct SerialPacket
{
    struct Inner
    {
        uint64_t deltaMicros;
        float ax, ay, az;
        float gx, gy, gz, gw;
        float ex, ey, ez;
        uint8_t padding[4];
    } __attribute__((packed)) inner;
    uint32_t crc32;
    uint64_t magic;
} __attribute__((packed));