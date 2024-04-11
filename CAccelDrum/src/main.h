#pragma once
#include <Arduino.h>
#include <Bounce2.h>
#include <MPU6050_6Axis_MotionApps20.h>

constexpr uint64_t oneSecMillis = 1000;
constexpr int buttonPin1 = 36;
constexpr int buttonPin2 = 39;
constexpr int ledPinDebug = 13;
constexpr int debugLedBrightness = 128;
extern MPU6050 mpu;
extern Bounce2::Button btn1;
extern Bounce2::Button btn2;
extern VectorFloat accumulatedPos;

void updateBtns();
void processDmpPacket();
void receivePacket();
void blinkLed();