#include <Arduino.h>
#include <cstring>
#include <cstdio>
#include <limits>
#include <I2Cdev.h>
#include <MPU6050.h>
#if I2CDEV_IMPLEMENTATION == I2CDEV_ARDUINO_WIRE
    #include "Wire.h"
#endif
#define SUPERVISION_CALLBACK
#define SUPERVISION_CALLBACK_TIMEOUT WDTO_1S
#include <DeepSleepScheduler.h>
#include <LiquidCrystal_I2C.h>
#include "main.h"
#include "Display/Display.h"
#include "Serial/SerialManager.h"
#include "Utils/Utils.h"
#include "Utils/PacketUtils.h"

// class default I2C address is 0x68
// specific I2C addresses may be passed as a parameter here
// AD0 low = 0x68 (default for SparkFun breakout and InvenSense evaluation board)
// AD0 high = 0x69
MPU6050 mpu;
//MPU6050 mpu(0x69); // <-- use for AD0 high

bool blinkState = false;
uint64_t lastBlinkMillis = 0;
Bounce2::Button btn1 = Bounce2::Button();
Bounce2::Button btn2 = Bounce2::Button();
VectorFloat accumulatedPos = VectorFloat();

// MPU control/status vars
bool dmpReady = false;  // set true if DMP init was successful
uint8_t mpuIntStatus;   // holds actual interrupt status byte from MPU
uint8_t devStatus;      // return status after each device operation (0 = success, !0 = error)
uint16_t packetSize;    // expected DMP packet size (default is 42 bytes)
uint16_t fifoCount;     // count of all bytes currently in FIFO
uint8_t fifoBuffer[64]; // FIFO storage buffer

// orientation/motion vars
Quaternion q;           // [w, x, y, z]         quaternion container
VectorInt16 aa;         // [x, y, z]            accel sensor measurements
VectorInt16 aaReal;     // [x, y, z]            gravity-free accel sensor measurements
VectorInt16 aaWorld;    // [x, y, z]            world-frame accel sensor measurements
VectorFloat gravity;    // [x, y, z]            gravity vector
float euler[3];         // [psi, theta, phi]    Euler angle container
float ypr[3];           // [yaw, pitch, roll]   yaw/pitch/roll container and gravity vector

volatile bool mpuInterrupt = false;
void dmpDataReady() {
    mpuInterrupt = true;
    display.print(display.cols - 1, 0, '+');
}

void setup() {
    pinMode(ledPinDebug, OUTPUT);
    analogWrite(ledPinDebug, debugLedBrightness);

    // join I2C bus (I2Cdev library doesn't do this automatically)
    #if I2CDEV_IMPLEMENTATION == I2CDEV_ARDUINO_WIRE
        Wire.begin();
        Wire.setClock(400000); // 400kHz I2C clock. Comment this line if having compilation difficulties
    #elif I2CDEV_IMPLEMENTATION == I2CDEV_BUILTIN_FASTWIRE
        Fastwire::setup(400, true);
    #endif
    
    display.init();
    display.setBacklight(true);
    serial.init();

    mpu.initialize(ACCEL_FS::A8G, GYRO_FS::G1000DPS);
    // pinMode(interruptPin, INPUT);

    // mpu.setXGyroOffset(220);
    // mpu.setYGyroOffset(76);
    // mpu.setZGyroOffset(-85);
    // mpu.setZAccelOffset(1788); // 1688 factory default for my test chip

    if (devStatus == 0)
    {
        mpu.CalibrateAccel(6);
        mpu.CalibrateGyro(6);

        // attachInterrupt(digitalPinToInterrupt(interruptPin), dmpDataReady, RISING);
        // mpuIntStatus = mpu.getIntStatus();
        // dmpReady = true;
    }
    else
    {
        // ERROR!
        // 1 = initial memory load failed
        // 2 = DMP configuration updates failed
        // (if it's going to break, usually the code will be 1)
        PacketUtils::printfToPackets("DMP Initialization failed (code %u)\n", devStatus);
    }

    pinMode(LED_BUILTIN, OUTPUT);
    btn1.attach(buttonPin1, INPUT); // Internal pullup isn't strong enough to not trigger interrupt
    btn1.interval(5);
    btn1.setPressedState(LOW);
    btn2.attach(buttonPin2, INPUT);
    btn2.interval(5);
    btn2.setPressedState(LOW);
    analogWrite(ledPinDebug, 0);
}

uint64_t lastSendMicros = 0;
std::array<RawAccelPacket::Pack, RawAccelPacket::packCount> batchingBuffer;
uint32_t batchingBufferIndex = 0;
uint32_t batchesSent = 0;

void processDmpPacket()
{
    scheduler.scheduleDelayed(processDmpPacket, 10);
    mpuInterrupt = false;
    display.print(display.cols - 1, 0, '\0');

    int16_t ax, ay, az;
    int16_t gx, gy, gz;

    mpu.getMotion6(&ax, &ay, &az, &gx, &gy, &gz);

    // mpu.dmpGetQuaternion(&q, fifoBuffer);
    // mpu.dmpGetAccel(&aa, fifoBuffer);
    // mpu.dmpGetGravity(&gravity, &q);
    // mpu.dmpGetLinearAccel(&aaReal, &aa, &gravity);
    // mpu.dmpGetEuler(euler, &q);
    // memcpy(&aaWorld, &aaReal, sizeof(VectorInt16)); // World accel
    // aaWorld.rotate(&q);
    uint64_t micro = micros();
    float ares = mpu.get_acce_resolution();
    float gres = mpu.get_gyro_resolution();
    RawAccelPacket::Pack pack =
    {
        .deltaMicros = (uint32_t)min(micro - lastSendMicros, (uint64_t)std::numeric_limits<uint32_t>::max()),
        .ax = ax * ares,
        .ay = ay * ares,
        .az = az * ares,
        .gx = gx * gres,
        .gy = gy * gres,
        .gz = gz * gres,
    };
    batchingBuffer[batchingBufferIndex++] = pack;
    if (batchingBufferIndex >= RawAccelPacket::packCount)
    {
        batchingBufferIndex = 0;
        RawAccelPacket packet =
        {
            .packs = batchingBuffer
        };
        PacketUtils::send(PacketType::RawAccel, packet);
        batchesSent++;
    }
    // AccelPacket packet =
    // {
    //     .deltaMicros = micro - lastSendMicros,
    //     .ax = aaWorld.x * mpu.get_acce_resolution(),
    //     .ay = aaWorld.y * mpu.get_acce_resolution(),
    //     .az = aaWorld.z * mpu.get_acce_resolution(),
    //     .gx = q.x * mpu.get_gyro_resolution(),
    //     .gy = q.y * mpu.get_gyro_resolution(),
    //     .gz = q.z * mpu.get_gyro_resolution(),
    //     .gw = q.w * mpu.get_gyro_resolution(),
    //     .ex = euler[0],
    //     .ey = euler[1],
    //     .ez = euler[2],
    // };
    lastSendMicros = micro;
    // PacketUtils::send(PacketType::Accel, packet);
    display.clear();
    display.printf(0, 1, "%u", batchesSent);
    // display.printf(0, 0, "% 5.0f% 5.0f% 5.0f",
    //     aaWorld.x * mpu.get_acce_resolution() * mpu.get_acce_resolution(),
    //     aaWorld.y * mpu.get_acce_resolution() * mpu.get_acce_resolution(),
    //     aaWorld.z * mpu.get_acce_resolution() * mpu.get_acce_resolution());
    // display.printf(0, 1, "% 5.0f% 5.0f% 5.0f",
    //     euler[0] / PI * 180,
    //     euler[1] / PI * 180,
    //     euler[2] / PI * 180);
    return;
}

void updateBtns()
{
    scheduler.schedule(updateBtns);
    btn1.update();
    btn2.update();
    if (btn1.pressed())
    {
        accumulatedPos = VectorFloat();
        bool bl = display.toggleBacklight();
        display.overlayClear();
        display.overlayPrintf(0, 0, 1000, bl ? "Backlight on" : "Backlight off");
        PacketUtils::printlnToPackets(bl ? "Backlight on" : "Backlight off");
    }
    if (btn2.pressed())
    {
        SerialPacket packet
        {
            .crc32 = 0x6969,
            .magic = 0xDEADBEEF80085069
        };
        serial.sendNative(packet);

        display.overlayClear();
        display.overlayPrintf(0, 0, 1000, "Garbage crc");
        display.overlayPrintf(display.cols - std::strlen("packet sent"), 1, 1000, "packet sent");
    }
}

void blinkLed()
{
    if (blinkState)
    {
        scheduler.scheduleDelayed(blinkLed, 1000);
        digitalWrite(LED_BUILTIN, blinkState = false);
    }
    else
    {
        scheduler.schedule(blinkLed);
        digitalWrite(LED_BUILTIN, blinkState = true);
    }
}

static void handleConfigurePacket(ConfigurePacket& packet)
{
    switch (packet.type)
    {
        case ConfigurePacket::Type::Backlight:
            switch (packet.value)
            {
                case ConfigurePacket::Val::BacklightGet:
                    PacketUtils::send(PacketType::Configure, ConfigurePacket{
                        ConfigurePacket::Type::Backlight,
                        display.getBacklight()
                            ? ConfigurePacket::Val::BacklightResultOn
                            : ConfigurePacket::Val::BacklightResultOff});
                    break;
                case ConfigurePacket::Val::BacklightSetOff:
                    display.setBacklight(false);
                    PacketUtils::sendConfigureAck(
                        ConfigurePacket::Type::Backlight,
                        ConfigurePacket::Val::BacklightAck);
                    break;
                case ConfigurePacket::Val::BacklightSetOn:
                    display.setBacklight(true);
                    PacketUtils::sendConfigureAck(
                        ConfigurePacket::Type::Backlight,
                        ConfigurePacket::Val::BacklightAck);
                    break;
                case ConfigurePacket::Val::BacklightSetToggle:
                    display.toggleBacklight();
                    PacketUtils::sendConfigureAck(
                        ConfigurePacket::Type::Backlight,
                        ConfigurePacket::Val::BacklightAck);
                    break;
            }
            break;
        case ConfigurePacket::Type::ResetDmp:
            mpu.resetDMP();
            mpu.resetFIFO();
            mpu.resetSensors();
            mpu.initialize(ACCEL_FS::A8G, GYRO_FS::G1000DPS);
            PacketUtils::sendConfigureAck(
                ConfigurePacket::Type::ResetDmp,
                ConfigurePacket::Val::ResetDmpAck);
            break;
    }
}

void receivePackets()
{
    scheduler.schedule(receivePackets);
    SerialPacket packet;
    if (serial.tryDequeueInbound(packet))
    {
        display.overlayClear();
        std::string s = Utils::stringSprintf("|%d|", serial.getPacketCount());
        display.overlayPrintf(display.cols - s.length(), 1, 1000, s.c_str());
        switch ((PacketType)packet.type)
        {
            case PacketType::Configure:
            {
                ConfigurePacket con = PacketUtils::innerAs<ConfigurePacket>(packet);
                handleConfigurePacket(con);
                break;
            }
        }
    }
}

class TaskTimeoutCallback : public Runnable
{
    void run()
    {
        while (1)
            ;
    }
};

static TaskTimeoutCallback timeoutCallback;

// Mpu6050 addr: 0x68
// Lcd addr: 0x27
void loop() {
    // if (!dmpReady) return;
    scheduler.setTaskTimeout(TIMEOUT_2S);
    scheduler.setSupervisionCallback(&timeoutCallback);
    scheduler.schedule(updateBtns);
    scheduler.schedule(processDmpPacket);
    scheduler.schedule(blinkLed);
    scheduler.schedule(receivePackets);
    scheduler.schedule(&display);
    scheduler.schedule(&serial);
    scheduler.execute();
}

