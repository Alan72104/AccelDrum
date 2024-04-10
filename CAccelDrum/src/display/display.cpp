#include <Arduino.h>
#include <DeepSleepScheduler.h>
#include <LiquidCrystal_I2C.h>
#include <cstring>
#include <cstdio>
#include "main.h"
#include "display.h"

LiquidCrystal_I2C lcd = LiquidCrystal_I2C(0x27, 20, 4);
uint64_t lcdOverlayTimeoutMillis = 0;
char lcdBufOverlay[lcdBufRows][lcdBufCols] = {0};
char lcdBufNew[lcdBufRows][lcdBufCols] = {0};
char lcdBufOld[lcdBufRows][lcdBufCols] = {0};

void displayInit()
{
    for (int row = 0; row < lcdBufRows; row++)
    {
        *reinterpret_cast<uint32_t*>(&lcdBufOld[row][lcdCols + 1]) = 0xDEADBEFF;
        *reinterpret_cast<uint32_t*>(&lcdBufNew[row][lcdCols + 1]) = 0xDEADBEFF;
        *reinterpret_cast<uint32_t*>(&lcdBufOverlay[row][lcdCols + 1]) = 0xDEADBEFF;
    }
    lcd.begin(16, 2);
    lcd.backlight();
}

void displayClear()
{
    for (int j = 0; j < lcdRows; j++)
        std::memset(lcdBufNew[j], '\0', lcdCols);
}

void displayOverlayClear()
{
    lcdOverlayTimeoutMillis = 0;
}

static bool displayBufPrintf(char buf[lcdBufRows][lcdBufCols], uint8_t col, uint8_t row, const char* s, va_list args)
{
    if (row >= lcdRows || col >= lcdCols)
        return false;
    int fullLen = std::vsnprintf(buf[row] + col, lcdCols + 1 - col, s, args);
    return fullLen >= 0 && fullLen < lcdCols + 1 - col;
}

bool displayPrintf(uint8_t col, uint8_t row, const char* s, ...)
{
    va_list args;
    va_start(args, s);
    bool res = displayBufPrintf(lcdBufNew, col, row, s, args);
    va_end(args);
    return res;
}

bool displayOverlayPrintf(uint8_t col, uint8_t row, uint64_t timeoutPeriodMs, const char* s, ...)
{
    lcdOverlayTimeoutMillis = millis() + timeoutPeriodMs;
    va_list args;
    va_start(args, s);
    bool res = displayBufPrintf(lcdBufOverlay, col, row, s, args);
    va_end(args);
    return res;
}

static bool displayBufPrint(char buf[lcdBufRows][lcdBufCols], uint8_t col, uint8_t row, const char c)
{
    if (col >= lcdCols || row >= lcdRows)
        return false;
    buf[row][col] = c;
    return true;
}

bool displayPrint(uint8_t col, uint8_t row, const char c)
{
    return displayBufPrint(lcdBufNew, col, row, c);
}

bool displayOverlayPrint(uint8_t col, uint8_t row, uint64_t timeoutPeriodMs, const char c)
{
    lcdOverlayTimeoutMillis = millis() + timeoutPeriodMs;
    return displayBufPrint(lcdBufNew, col, row, c);
}

bool displayCheckBufIntegrity()
{
    for (int j = 0; j < lcdRows; j++)
    {
        if (lcdBufOld[j][lcdCols] != '\0' ||
            lcdBufNew[j][lcdCols] != '\0' ||
            *reinterpret_cast<uint32_t*>(&lcdBufOld[j][lcdCols + 1]) != 0xDEADBEFF ||
            *reinterpret_cast<uint32_t*>(&lcdBufNew[j][lcdCols + 1]) != 0xDEADBEFF ||
            *reinterpret_cast<uint32_t*>(&lcdBufOverlay[j][lcdCols + 1]) != 0xDEADBEFF)
        {
            lcdOverlayTimeoutMillis = 0;
            std::strcpy(lcdBufOld[0] + lcdCols - std::strlen("buffer"), "buffer");
            std::strcpy(lcdBufOld[1] + lcdCols - std::strlen("corrupt"), "corrupt");
            std::strcpy(lcdBufNew[0] + lcdCols - std::strlen("buffer"), "buffer");
            std::strcpy(lcdBufNew[1] + lcdCols - std::strlen("corrupt"), "corrupt");
            return false;
        }
    }
    return true;
}

void displayUpdate()
{
    scheduler.scheduleDelayed(displayUpdate, 100);
    
    bool corrupted = !displayCheckBufIntegrity();
    if (corrupted)
    {
        analogWrite(ledPinDebug, debugLedBrightness);
        lcd.setCursor(0, 0);
        lcd.print(lcdBufNew[0]);
        lcd.setCursor(0, 1);
        lcd.print(lcdBufNew[1]);
        while (true);
    }
    else if (lcdOverlayTimeoutMillis ||
        std::memcmp(lcdBufOld, lcdBufNew, (lcdBufCols * lcdBufRows)) != 0)
    {
        for (int j = 0; j < lcdBufRows; j++)
            std::memcpy(lcdBufOld[j], lcdBufNew[j], lcdCols);

        if (lcdOverlayTimeoutMillis)
        {
            if (millis() < lcdOverlayTimeoutMillis)
            {
                for (int j = 0; j < lcdBufRows; j++)
                    for (int i = 0; i < lcdCols; i++)
                    {
                        const char car = lcdBufOverlay[j][i];
                        if (car != '\0')
                            lcdBufOld[j][i] = car;
                    }
            }
            else
                displayOverlayClear();
        }

        lcd.setCursor(0, 0);
        lcd.print(lcdBufOld[0]);
        lcd.setCursor(0, 1);
        lcd.print(lcdBufOld[1]);
    }
}