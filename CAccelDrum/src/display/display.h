#pragma once
#include <Arduino.h>
#include <LiquidCrystal_I2C.h>
#define LIBCALL_DEEP_SLEEP_SCHEDULER
#include <DeepSleepScheduler.h>

class Display;
extern Display display;

class Display : public Runnable
{
public:
    static constexpr uint32_t cols = 16;
    static constexpr uint32_t bufCols = cols + 1 + sizeof(uint32_t);
    static constexpr uint32_t rows = 2;
    static constexpr uint32_t bufRows = rows;

    Display();

    // Sets the magic numbers at the end of each row and starts the driver
    void init();

    virtual void run() override;

    // Returns true if all 3 dispaly buffers' trailing null char and magic number aren't overridden,
    // when corruption is detected, also writes "buffer" "corrupt" to the end,
    // and resets the trailing null char and magic number,
    // execution should not continue after the corruption
    bool checkBufIntegrity();

    // Sends update to the display when needed, also checks for buffer corruption
    void update();

    // Clears the display
    void clear();

    // Sets the backlight
    void setBacklight(bool v);

    // Toggles the backlight
    bool toggleBacklight();

    // Gets the backlight
    bool getBacklight() const;

    // Returns true if col and row and final string length are within the range,
    // string is truncated if length is longer than cols
    bool printf(uint32_t col, uint32_t row, const char* s, ...);

    // Returns true if col and row are within the range
    bool print(uint32_t col, uint32_t row, const char c);

    // Sets everything in the overlay to '\0' and clears the timeout
    void overlayClear();

    // Returns true if col and row and final string length are within the range,
    // string is truncated if length is longer than cols
    // Display will be updated constantly before the timeout elapses
    bool overlayPrintf(uint32_t col, uint32_t row, uint64_t timeoutPeriodMs, const char *s, ...);

    // Returns true if col and row are within the range
    // Display will be updated constantly before the timeout elapses
    bool overlayPrint(uint32_t col, uint32_t row, uint64_t timeoutPeriodMs, const char c);

private:
    LiquidCrystal_I2C lcd;
    uint64_t lcdOverlayTimeoutMillis;
    char lcdBufOverlay[bufRows][bufCols]; // '\0' in the overlay means transparent
    char lcdBufNew[bufRows][bufCols];
    char lcdBufOld[bufRows][bufCols];
    bool backlight;

    bool bufPrintf(char buf[bufRows][bufCols], uint32_t col, uint32_t row, const char* s, va_list args);
    bool bufPrint(char buf[bufRows][bufCols], uint32_t col, uint32_t row, const char c);
};