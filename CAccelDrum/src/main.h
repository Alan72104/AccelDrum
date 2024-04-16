#pragma once
#include <Arduino.h>
#include <Bounce2.h>
#include <MPU6050_6Axis_MotionApps20.h>

constexpr uint64_t oneSecMillis = 1000;
constexpr uint32_t buttonPin1 = 36;
constexpr uint32_t buttonPin2 = 39;
constexpr uint32_t ledPinDebug = 13;
constexpr uint32_t debugLedBrightness = 128;
constexpr uint32_t interruptPin = 15;
extern MPU6050 mpu;
extern Bounce2::Button btn1;
extern Bounce2::Button btn2;
extern VectorFloat accumulatedPos;

void updateBtns();
void processDmpPacket();
void receivePackets();
void blinkLed();