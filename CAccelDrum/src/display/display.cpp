#include <Arduino.h>
#define LIBCALL_DEEP_SLEEP_SCHEDULER
#include <DeepSleepScheduler.h>
#include <LiquidCrystal_I2C.h>
#include <cstring>
#include <cstdio>
#include <string_view>
#include "main.h"
#include "Display.h"
#include "Utils/Utils.h"

Display display;

Display::Display() : lcd(0x27, 20, 4),
                     lcdOverlayTimeoutMillis(0),
                     lcdBufOverlay{},
                     lcdBufNew{},
                     lcdBufOld{},
                     backlight(false),
                     theLock()
{
    init();
}

void Display::init()
{
    for (uint32_t row = 0; row < bufRows; row++)
    {
        memset(lcdBufOld[row], ' ', cols);
        memset(lcdBufNew[row], ' ', cols);
        *reinterpret_cast<uint32_t*>(&lcdBufOld[row][cols + 1]) = 0xDEADBEFF;
        *reinterpret_cast<uint32_t*>(&lcdBufNew[row][cols + 1]) = 0xDEADBEFF;
        *reinterpret_cast<uint32_t*>(&lcdBufOverlay[row][cols + 1]) = 0xDEADBEFF;
    }
    lcd.begin(16, 2);
}

void Display::clear()
{
    for (uint32_t j = 0; j < rows; j++)
        memset(lcdBufNew[j], ' ', cols);
}

void Display::setBacklight(bool v)
{
    if (v)
        lcd.backlight();
    else
        lcd.noBacklight();
    backlight = v;
}

bool Display::toggleBacklight()
{
    backlight = !backlight;
    if (backlight)
        lcd.backlight();
    else
        lcd.noBacklight();
    return backlight;
}

bool Display::getBacklight() const
{
    return backlight;
}

ScopedLocker Display::lock()
{
    return theLock.lock();
}

void Display::overlayClear()
{
    lcdOverlayTimeoutMillis = 0;
    for (uint32_t j = 0; j < rows; j++)
        memset(lcdBufOverlay[j], '\0', cols);
}

bool Display::bufPrintf(char buf[bufRows][bufCols], uint32_t col, uint32_t row, const char* s, va_list args)
{
    if (row >= rows || col >= cols)
        return false;
    uint32_t printBufSize = cols - col + 1;
    char printBuf[printBufSize] = {0};
    int32_t fullLen = vsnprintf(printBuf, printBufSize, s, args);
    // Don't print directly to avoid writing the terminating null
    memcpy(buf[row] + col, printBuf, min(fullLen, (int32_t)printBufSize - 1) * sizeof(char));
    return fullLen >= 0 && fullLen < printBufSize;
}

bool Display::printf(uint32_t col, uint32_t row, const char* s, ...)
{
    va_list args;
    va_start(args, s);
    bool res = bufPrintf(lcdBufNew, col, row, s, args);
    va_end(args);
    return res;
}

bool Display::overlayPrintf(uint32_t col, uint32_t row, uint64_t timeoutPeriodMs, const char* s, ...)
{
    lcdOverlayTimeoutMillis = millis() + timeoutPeriodMs;
    va_list args;
    va_start(args, s);
    bool res = bufPrintf(lcdBufOverlay, col, row, s, args);
    va_end(args);
    return res;
}

bool Display::bufPrint(char buf[bufRows][bufCols], uint32_t col, uint32_t row, const char c)
{
    if (col >= cols || row >= rows)
        return false;
    buf[row][col] = c;
    return true;
}

bool Display::print(uint32_t col, uint32_t row, const char c)
{
    return bufPrint(lcdBufNew, col, row, c);
}

bool Display::overlayPrint(uint32_t col, uint32_t row, uint64_t timeoutPeriodMs, const char c)
{
    lcdOverlayTimeoutMillis = millis() + timeoutPeriodMs;
    return bufPrint(lcdBufOverlay, col, row, c);
}

bool Display::checkBufIntegrity()
{
    for (uint32_t j = 0; j < rows; j++)
    {
        if (lcdBufOld[j][cols] != '\0' ||
            lcdBufNew[j][cols] != '\0' ||
            lcdBufOverlay[j][cols] != '\0' ||
            memchr(lcdBufOld[j], '\0', cols) ||
            memchr(lcdBufNew[j], '\0', cols) ||
            *reinterpret_cast<uint32_t*>(&lcdBufOld[j][cols + 1]) != 0xDEADBEFF ||
            *reinterpret_cast<uint32_t*>(&lcdBufNew[j][cols + 1]) != 0xDEADBEFF ||
            *reinterpret_cast<uint32_t*>(&lcdBufOverlay[j][cols + 1]) != 0xDEADBEFF)
        {
            lcdOverlayTimeoutMillis = 0;
            strcpy(lcdBufOld[0] + cols - strlen("buffer"), "buffer");
            strcpy(lcdBufOld[1] + cols - strlen("corrupt"), "corrupt");
            strcpy(lcdBufNew[0] + cols - strlen("buffer"), "buffer");
            strcpy(lcdBufNew[1] + cols - strlen("corrupt"), "corrupt");
            return false;
        }
    }
    return true;
}

void Display::update()
{
    bool corrupted = !checkBufIntegrity();
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
        memcmp(lcdBufOld, lcdBufNew, (bufCols * bufRows)) != 0)
    {
        for (uint32_t j = 0; j < bufRows; j++)
            memcpy(lcdBufOld[j], lcdBufNew[j], cols);

        if (lcdOverlayTimeoutMillis)
        {
            if (millis() < lcdOverlayTimeoutMillis)
            {
                for (uint32_t j = 0; j < bufRows; j++)
                    for (uint32_t i = 0; i < cols; i++)
                    {
                        const char car = lcdBufOverlay[j][i];
                        if (car != '\0')
                            lcdBufOld[j][i] = car;
                    }
            }
            else
                overlayClear();
        }

        lcd.setCursor(0, 0);
        lcd.print(lcdBufOld[0]);
        lcd.setCursor(0, 1);
        lcd.print(lcdBufOld[1]);
    }
}

void Display::run()
{
    scheduler.scheduleDelayed(this, 100);
    update();
}