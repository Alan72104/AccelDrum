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

// Mpu6050 addr: 0x68, 0x69
// Lcd addr: 0x27
MPU6050 mpus[4] =
{
    MPU6050(0x68),
    MPU6050(0x69),
    MPU6050(0x68, &Wire1),
    MPU6050(0x69, &Wire1),
};

bool blinkState = false;
uint64_t lastBlinkMillis = 0;
Bounce2::Button btn1;
Bounce2::Button btn2;
uint64_t lastPollMillis = 0;

void setup() {
    pinMode(ledPinDebug, OUTPUT);
    analogWrite(ledPinDebug, debugLedBrightness);

    // join I2C bus (I2Cdev library doesn't do this automatically)
    #if I2CDEV_IMPLEMENTATION == I2CDEV_ARDUINO_WIRE
        Wire.begin();
        Wire.setClock(400000);
        Wire1.begin(wire1Sda, wire1Scl);
        Wire1.setClock(400000);
    #elif I2CDEV_IMPLEMENTATION == I2CDEV_BUILTIN_FASTWIRE // TODO: Remove this
        Fastwire::setup(400, true);
    #endif
    
    display.init();
    display.setBacklight(true);
    serial.init();

    display.clear();
    display.printf(0, 0, "Init mpu");
    display.update();
    for (MPU6050 &mpu : mpus)
        mpu.initialize(ACCEL_FS::A8G, GYRO_FS::G1000DPS);

    display.clear();
    display.printf(0, 0, "Mpu set offset");
    display.update();
    for (MPU6050 &mpu : mpus)
    {
        mpu.setXGyroOffset(220);
        mpu.setYGyroOffset(76);
        mpu.setZGyroOffset(-85);
        mpu.setZAccelOffset(1788); // 1688 factory default for my test chip
    }

    display.clear();
    display.printf(0, 0, "Mpu calibrate");
    display.printf(0, 1, "?-?-?-?");
    display.update();
    static auto calibrate = [](uint8_t i)
    {
        mpus[i].CalibrateAccel(6);
        mpus[i].CalibrateGyro(6);
        {
            auto lock = display.lock();
            display.print(i * 2, 1, 'v');
            display.update();
        }
    };
    static volatile bool otherHalfDone = false;
    auto doOtherHalf = [](void *data) {
        calibrate(2);
        calibrate(3);
        otherHalfDone = true;
        vTaskDelete(nullptr);
    };
    TaskHandle_t task;
    xTaskCreatePinnedToCore(doOtherHalf, "Mpu calibration", 2048, nullptr, 10, &task, 0);
    calibrate(0);
    calibrate(1);
    while (!otherHalfDone)
        ;
    
    display.printf(0, 0, "Set pins");
    display.update();
    pinMode(LED_BUILTIN, OUTPUT);
    btn1.attach(buttonPin1, INPUT); // Internal pullup isn't strong enough to not trigger interrupt
    btn1.interval(5);
    btn1.setPressedState(LOW);
    btn2.attach(buttonPin2, INPUT);
    btn2.interval(5);
    btn2.setPressedState(LOW);
    analogWrite(ledPinDebug, 0);
}

uint64_t lastSendMicros[4];
uint32_t batchesSent = 0;

void processDmpPacket()
{
    display.clear();

    if (millis() - lastPollMillis <= 1500)
    {
        RawAccelPacket packet = {0};

        auto getData = [&](uint8_t i)
        {
            uint64_t micro = micros();
            int16_t ax, ay, az;
            int16_t gx, gy, gz;
            mpus[i].getMotion6(&ax, &ay, &az, &gx, &gy, &gz);
            float ares = mpus[i].get_acce_resolution();
            float gres = mpus[i].get_gyro_resolution();
            RawAccelPacket::Pack pack =
            {
                .deltaMicros = (uint32_t)min(micro - lastSendMicros[i], (uint64_t)std::numeric_limits<uint32_t>::max()),
                .ax = ax * ares,
                .ay = ay * ares,
                .az = az * ares,
                .gx = gx * gres,
                .gy = gy * gres,
                .gz = gz * gres,
            };
            packet.packs[i] = pack;
            lastSendMicros[i] = micro;
        };
        for (uint8_t i = 0; i < 4; i++)
            getData(i);
        PacketUtils::send(PacketType::RawAccel, packet);
        batchesSent++;

        display.print(display.cols - 1, 1, '*');
        
        scheduler.scheduleDelayed(processDmpPacket, 10);
    }
    else
    {
        scheduler.scheduleDelayed(processDmpPacket, 100);
    }

    uint32_t secs = millis() / 1000;
    char numSentMetric[10];
    Utils::formatMetric(numSentMetric, 10, batchesSent);
    display.printf(0, 1, "%u:%u:%u %s", secs / 3600, secs / 60 % 60, secs % 60, numSentMetric);
    return;
}

void updateBtns()
{
    scheduler.schedule(updateBtns);
    btn1.update();
    btn2.update();
    if (btn1.pressed())
    {
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
        display.overlayPrintf(display.cols - strlen("packet sent"), 1, 1000, "packet sent");

        lastPollMillis = millis();
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
        case ConfigurePacket::Type::PollForData:
            lastPollMillis = millis();
            break;
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
        case ConfigurePacket::Type::Reset:
        {
            std::array<uint8_t, 12> accelTrims;
            std::array<uint8_t, 12> gyroTrims;
            for (uint32_t i = 0; i < 4; i++)
            {
                accelTrims[i * 3 + 0] = mpus[i].getAccelXSelfTestFactoryTrim();
                accelTrims[i * 3 + 1] = mpus[i].getAccelYSelfTestFactoryTrim();
                accelTrims[i * 3 + 2] = mpus[i].getAccelZSelfTestFactoryTrim();
                gyroTrims[i * 3 + 0] = mpus[i].getGyroXSelfTestFactoryTrim();
                gyroTrims[i * 3 + 1] = mpus[i].getGyroYSelfTestFactoryTrim();
                gyroTrims[i * 3 + 2] = mpus[i].getGyroZSelfTestFactoryTrim();
            }
            
            ConfigurePacket packet =
            {
                ConfigurePacket::Type::Reset,
                ConfigurePacket::Val::ResetResultSettings
            };

            PacketUtils::getConfigureDataAs<ConfigurePacket::Settings>(packet.data) =
            {
                .accelRange = mpus[0].getFullScaleAccelRange(),
                .gyroRange = mpus[0].getFullScaleGyroRange(),
                .accelFactoryTrims = accelTrims,
                .gyroFactoryTrims = gyroTrims
            };
            
            PacketUtils::sendConfigureAck(
                ConfigurePacket::Type::Reset,
                ConfigurePacket::Val::ResetAck);
            PacketUtils::send(PacketType::Configure, packet);
            break;
        }
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
        display.overlayPrintf(display.cols - s.length(), 0, 1000, s.c_str());
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
    void run() override
    {
        while (1)
            ;
    }
};

static TaskTimeoutCallback timeoutCallback;

void loop() {
    display.printf(0, 0, "Set sched");
    display.update();
    scheduler.setTaskTimeout(TIMEOUT_2S);
    scheduler.setSupervisionCallback(&timeoutCallback);
    scheduler.schedule(updateBtns);
    scheduler.schedule(processDmpPacket);
    scheduler.schedule(blinkLed);
    scheduler.schedule(receivePackets);
    scheduler.schedule(&display);
    scheduler.schedule(&serial);
    display.printf(0, 0, "Sched run");
    display.update();
    scheduler.execute(); // Does not return
}

