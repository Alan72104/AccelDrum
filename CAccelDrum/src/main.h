#pragma once
#include <Arduino.h>
#include <Bounce2.h>
#include <MPU6050.h>

constexpr uint64_t oneSecMillis = 1000;
constexpr uint32_t buttonPin1 = 36;
constexpr uint32_t buttonPin2 = 39;
constexpr uint32_t ledPinDebug = 13;
constexpr uint32_t debugLedBrightness = 128;
constexpr uint32_t interruptPin = 15;
constexpr uint32_t wire1Scl = 17;
constexpr uint32_t wire1Sda = 16;
extern MPU6050 mpus[4];
extern Bounce2::Button btn1;
extern Bounce2::Button btn2;

void updateBtns();
void processDmpPacket();
void receivePackets();
void blinkLed();