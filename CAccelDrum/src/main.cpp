#include <Arduino.h>
#include <cstring>
#include <cstdio>
#include <I2Cdev.h>
#include <MPU6050_6Axis_MotionApps20.h>
//#include "MPU6050.h" // not necessary if using MotionApps include file
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

// class default I2C address is 0x68
// specific I2C addresses may be passed as a parameter here
// AD0 low = 0x68 (default for SparkFun breakout and InvenSense evaluation board)
// AD0 high = 0x69
MPU6050 mpu;
//MPU6050 mpu(0x69); // <-- use for AD0 high

/* =========================================================================
   NOTE: In addition to connection 3.3v, GND, SDA, and SCL, this sketch
   depends on the MPU-6050's INT pin being connected to the Arduino's
   external interrupt #0 pin. On the Arduino Uno and Mega 2560, this is
   digital I/O pin 2.
 * ========================================================================= */

// uncomment "OUTPUT_READABLE_QUATERNION" if you want to see the actual
// quaternion components in a [w, x, y, z] format (not best for parsing
// on a remote host such as Processing or something though)
//#define OUTPUT_READABLE_QUATERNION

// uncomment "OUTPUT_READABLE_EULER" if you want to see Euler angles
// (in degrees) calculated from the quaternions coming from the FIFO.
// Note that Euler angles suffer from gimbal lock (for more info, see
// http://en.wikipedia.org/wiki/Gimbal_lock)
//#define OUTPUT_READABLE_EULER

// uncomment "OUTPUT_READABLE_YAWPITCHROLL" if you want to see the yaw/
// pitch/roll angles (in degrees) calculated from the quaternions coming
// from the FIFO. Note this also requires gravity vector calculations.
// Also note that yaw/pitch/roll angles suffer from gimbal lock (for
// more info, see: http://en.wikipedia.org/wiki/Gimbal_lock)
// #define OUTPUT_READABLE_YAWPITCHROLL

// uncomment "OUTPUT_READABLE_REALACCEL" if you want to see acceleration
// components with gravity removed. This acceleration reference frame is
// not compensated for orientation, so +X is always +X according to the
// sensor, just without the effects of gravity. If you want acceleration
// compensated for orientation, us OUTPUT_READABLE_WORLDACCEL instead.
#define OUTPUT_READABLE_REALACCEL

// uncomment "OUTPUT_READABLE_WORLDACCEL" if you want to see acceleration
// components with gravity removed and adjusted for the world frame of
// reference (yaw is relative to initial orientation, since no magnetometer
// is present in this case). Could be quite handy in some cases.
// #define OUTPUT_READABLE_WORLDACCEL

// uncomment "OUTPUT_TEAPOT" if you want output that matches the
// format used for the InvenSense teapot demo
//#define OUTPUT_TEAPOT

#define INTERRUPT_PIN 15  // use pin 2 on Arduino Uno & most boards
#define LED_PIN 13 // (Arduino is 13, Teensy is 11, Teensy++ is 6)

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

// packet structure for InvenSense teapot demo
uint8_t teapotPacket[14] = { '$', 0x02, 0,0, 0,0, 0,0, 0,0, 0x00, 0x00, '\r', '\n' };

volatile bool mpuInterrupt = false;     // indicates whether MPU interrupt pin has gone high
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
        // Wire.setClock(200000); // 400kHz I2C clock. Comment this line if having compilation difficulties
    #elif I2CDEV_IMPLEMENTATION == I2CDEV_BUILTIN_FASTWIRE
        Fastwire::setup(400, true);
    #endif
    
    display.init();
    display.setBacklight(true);
    serial.init();

    // initialize device
    // Serial.println(F("Initializing I2C devices..."));
    mpu.initialize(ACCEL_FS::A8G, GYRO_FS::G1000DPS);
    // mpu.initialize(ACCEL_FS::A2G, GYRO_FS::G250DPS);
    pinMode(INTERRUPT_PIN, INPUT);

    // verify connection
    // Serial.println(F("Testing device connections..."));
    // Serial.println(mpu.testConnection() ? F("MPU6050 connection successful") : F("MPU6050 connection failed"));

    // wait for ready
    // Serial.println(F("\nSend any character to begin DMP programming and demo: "));
    // while (!Serial.available());                 // wait for data
    // while (Serial.available() && Serial.read()); // empty buffer again

    // load and configure the DMP
    // Serial.println(F("Initializing DMP..."));
    devStatus = mpu.dmpInitialize();

    // supply your own gyro offsets here, scaled for min sensitivity
    mpu.setXGyroOffset(220);
    mpu.setYGyroOffset(76);
    mpu.setZGyroOffset(-85);
    mpu.setZAccelOffset(1788); // 1688 factory default for my test chip

    // make sure it worked (returns 0 if so)
    if (devStatus == 0) {
        // Calibration Time: generate offsets and calibrate our MPU6050
        mpu.CalibrateAccel(6);
        mpu.CalibrateGyro(6);
        // mpu.PrintActiveOffsets();
        // turn on the DMP, now that it's ready
        // Serial.println(F("Enabling DMP..."));
        mpu.setDMPEnabled(true);
        // mpu.setFIFOTimeout(MPU6050_FIFO_DEFAULT_TIMEOUT);
        mpu.setFIFOTimeout(1000);

        // enable Arduino interrupt detection
        // Serial.print(F("Enabling interrupt detection (Arduino external interrupt "));
        // Serial.print(digitalPinToInterrupt(INTERRUPT_PIN));
        // Serial.println(F(")..."));
        attachInterrupt(digitalPinToInterrupt(INTERRUPT_PIN), dmpDataReady, RISING);
        mpuIntStatus = mpu.getIntStatus();

        // set our DMP Ready flag so the main loop() function knows it's okay to use it
        // Serial.println(F("DMP ready! Waiting for first interrupt..."));
        dmpReady = true;

        // get expected DMP packet size for later comparison
        packetSize = mpu.dmpGetFIFOPacketSize();

        // mpu.setFullScaleAccelRange(MPU6050_ACCEL_FS_2);
        // mpu.setFullScaleGyroRange(MPU6050_GYRO_FS_250);
    } else {
        // ERROR!
        // 1 = initial memory load failed
        // 2 = DMP configuration updates failed
        // (if it's going to break, usually the code will be 1)
        Serial.print(F("DMP Initialization failed (code "));
        Serial.print(devStatus);
        Serial.println(F(")"));
    }

    // configure LED for output
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

void processDmpPacket()
{
    scheduler.schedule(processDmpPacket);
    if (!mpuInterrupt)
        return;
    if (!mpu.dmpGetCurrentFIFOPacket(fifoBuffer))
        return;
    mpuInterrupt = false;
    display.print(display.cols - 1, 0, '\0');

    bool sendData = false;
    sendData = true;

    if (sendData)
    {
        mpu.dmpGetQuaternion(&q, fifoBuffer);
        mpu.dmpGetAccel(&aa, fifoBuffer);
        mpu.dmpGetGravity(&gravity, &q);
        mpu.dmpGetLinearAccel(&aaReal, &aa, &gravity);
        mpu.dmpGetEuler(euler, &q);
        memcpy(&aaWorld, &aaReal, sizeof(VectorInt16)); // World accel
        aaWorld.rotate(&q);
        uint64_t micro = micros();
        AccelPacket packet
        {
            .deltaMicros = micro - lastSendMicros,
            .ax = aaWorld.x * mpu.get_acce_resolution(),
            .ay = aaWorld.y * mpu.get_acce_resolution(),
            .az = aaWorld.z * mpu.get_acce_resolution(),
            .gx = q.x * mpu.get_gyro_resolution(),
            .gy = q.y * mpu.get_gyro_resolution(),
            .gz = q.z * mpu.get_gyro_resolution(),
            .gw = q.w * mpu.get_gyro_resolution(),
            .ex = euler[0],
            .ey = euler[1],
            .ez = euler[2],
        };
        lastSendMicros = micro;
        serial.send(PacketType::Accel, &packet);
        // displayPrintf(0, "% 5.0f% 5.0f% 5.0f",
        //     packet.gx / PI * 180,
        //     packet.gy / PI * 180,
        //     packet.gz / PI * 180);
        display.clear();
        display.printf(0, 0, "% 5.0f% 5.0f% 5.0f",
            aaWorld.x * mpu.get_acce_resolution() * mpu.get_acce_resolution(),
            aaWorld.y * mpu.get_acce_resolution() * mpu.get_acce_resolution(),
            aaWorld.z * mpu.get_acce_resolution() * mpu.get_acce_resolution());
        display.printf(0, 1, "% 5.0f% 5.0f% 5.0f",
            euler[0] / PI * 180,
            euler[1] / PI * 180,
            euler[2] / PI * 180);
        // float elapsedSecs = packet.deltaMicros / 1000000.0f;
        // accumulatedPos.x += aaWorld.x * elapsedSecs * elapsedSecs * mpu.get_acce_resolution() * 100;
        // accumulatedPos.y += aaWorld.y * elapsedSecs * elapsedSecs * mpu.get_acce_resolution() * 100;
        // accumulatedPos.z += aaWorld.z * elapsedSecs * elapsedSecs * mpu.get_acce_resolution() * 100;
        // displayPrintf(1, "% 5.0f% 5.0f% 5.0f",
        //     accumulatedPos.x,
        //     accumulatedPos.y,
        //     accumulatedPos.z);
        return;
    }
    else
    {
        #ifdef OUTPUT_READABLE_QUATERNION
            // display quaternion values in easy matrix form: w x y z
            mpu.dmpGetQuaternion(&q, fifoBuffer);
            Serial.print("quat\t");
            Serial.print(q.w);
            Serial.print("\t");
            Serial.print(q.x);
            Serial.print("\t");
            Serial.print(q.y);
            Serial.print("\t");
            Serial.println(q.z);
        #endif

        #ifdef OUTPUT_READABLE_EULER
            // display Euler angles in degrees
            mpu.dmpGetQuaternion(&q, fifoBuffer);
            mpu.dmpGetEuler(euler, &q);
            Serial.print("euler\t");
            Serial.print(euler[0] * 180/M_PI);
            Serial.print("\t");
            Serial.print(euler[1] * 180/M_PI);
            Serial.print("\t");
            Serial.println(euler[2] * 180/M_PI);
        #endif

        #ifdef OUTPUT_READABLE_YAWPITCHROLL
            // display Euler angles in degrees
            mpu.dmpGetQuaternion(&q, fifoBuffer);
            mpu.dmpGetGravity(&gravity, &q);
            mpu.dmpGetYawPitchRoll(ypr, &q, &gravity);
            Serial.print("ypr\t");
            Serial.print(ypr[0] * 180/M_PI);
            Serial.print("\t");
            Serial.print(ypr[1] * 180/M_PI);
            Serial.print("\t");
            Serial.println(ypr[2] * 180/M_PI);
        #endif

        #ifdef OUTPUT_READABLE_REALACCEL
            // display real acceleration, adjusted to remove gravity
            mpu.dmpGetQuaternion(&q, fifoBuffer);
            mpu.dmpGetAccel(&aa, fifoBuffer);
            mpu.dmpGetGravity(&gravity, &q);
            mpu.dmpGetLinearAccel(&aaReal, &aa, &gravity);
            Serial.printf("areal: % 6.2f, % 6.2f, % 6.2f\n",
                aaReal.x * mpu.get_acce_resolution(),
                aaReal.y * mpu.get_acce_resolution(),
                aaReal.z * mpu.get_acce_resolution()
            );
        #endif

        #ifdef OUTPUT_READABLE_WORLDACCEL
            // display initial world-frame acceleration, adjusted to remove gravity
            // and rotated based on known orientation from quaternion
            mpu.dmpGetQuaternion(&q, fifoBuffer);
            mpu.dmpGetAccel(&aa, fifoBuffer);
            mpu.dmpGetGravity(&gravity, &q);
            mpu.dmpGetLinearAccel(&aaReal, &aa, &gravity);
            mpu.dmpGetLinearAccelInWorld(&aaWorld, &aaReal, &q); // Where is this function?
            Serial.print("aworld\t");
            Serial.print(aaWorld.x);
            Serial.print("\t");
            Serial.print(aaWorld.y);
            Serial.print("\t");
            Serial.println(aaWorld.z);
        #endif

        #ifdef OUTPUT_TEAPOT
            // display quaternion values in InvenSense Teapot demo format:
            teapotPacket[2] = fifoBuffer[0];
            teapotPacket[3] = fifoBuffer[1];
            teapotPacket[4] = fifoBuffer[4];
            teapotPacket[5] = fifoBuffer[5];
            teapotPacket[6] = fifoBuffer[8];
            teapotPacket[7] = fifoBuffer[9];
            teapotPacket[8] = fifoBuffer[12];
            teapotPacket[9] = fifoBuffer[13];
            Serial.write(teapotPacket, 14);
            teapotPacket[11]++; // packetCount, loops at 0xFF on purpose
        #endif
    }
}

void updateBtns()
{
    scheduler.schedule(updateBtns);
    btn1.update();
    btn2.update();
    if (btn1.pressed())
    {
        // digitalWrite(LED_BUILTIN, blinkState = true);
        accumulatedPos = VectorFloat();
        bool bl = display.toggleBacklight();
        display.overlayClear();
        display.overlayPrintf(0, 0, 1000, bl ? "Backlight on" : "Backlight off");
    }
    if (btn2.pressed())
    {
        // digitalWrite(LED_BUILTIN, blinkState = false);
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
        // scheduler.scheduleDelayed(blinkLed, 100);
        digitalWrite(LED_BUILTIN, blinkState = true);
    }
}

class SupervisionCallback: public Runnable
{
    void run()
    {
        // this method is called from the interrupt so
        // delay() does not work
        
        // to see that the LED is on,
        // we block until the watchdog resets the CPU what
        // is configured with SUPERVISION_CALLBACK_TIMEOUT
        // and defaults to 1s
        while (1);
    }
};

// Mpu6050 addr: 0x68
// Lcd addr: 0.27
void loop() {
    if (!dmpReady) return;
    scheduler.setTaskTimeout(TIMEOUT_4S);
    scheduler.setSupervisionCallback(new SupervisionCallback());
    scheduler.schedule(updateBtns);
    scheduler.schedule(processDmpPacket);
    scheduler.schedule(blinkLed);
    scheduler.schedule(&display);
    scheduler.schedule(&serial);
    // scheduler.schedule(static_cast<Runnable*>(&display));
    scheduler.execute();
}

