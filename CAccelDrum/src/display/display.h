#pragma once
#include <Arduino.h>
#include <LiquidCrystal_I2C.h>

constexpr uint8_t lcdCols = 16;
constexpr uint8_t lcdBufCols = lcdCols + 1 + sizeof(uint32_t);
constexpr uint8_t lcdRows = 2;
constexpr uint8_t lcdBufRows = lcdRows;
extern LiquidCrystal_I2C lcd;
extern uint64_t lcdOverlayTimeoutMillis;
extern char lcdBufOverlay[lcdBufRows][lcdBufCols]; // '\0' in the overlay means transparent
extern char lcdBufNew[lcdBufRows][lcdBufCols];
extern char lcdBufOld[lcdBufRows][lcdBufCols];

// Sets the magic numbers at the end of each row and starts the driver
void displayInit();

// Return true if all 3 dispaly buffers' trailing null char and magic number aren't overridden,
// when corruption is detected, also writes "buffer" "corrupt" to the end,
// and resets the trailing null char and magic number,
// execution should not continue after the corruption
bool displayCheckBufIntegrity();

// Sends update to the display when needed, also checks for buffer corruption
void displayUpdate();

// Clears the display
void displayClear();

// Returns true if col and row and final string length are within the range,
// string is truncated if length is longer than cols
bool displayPrintf(uint8_t col, uint8_t row, const char* s, ...);

// Returns true if col and row are within the range
bool displayPrint(uint8_t col, uint8_t row, const char c);

// Sets everything in the overlay to '\0' and clears the timeout
void displayOverlayClear();

// Returns true if col and row and final string length are within the range,
// string is truncated if length is longer than cols
// Display will be updated constantly before the timeout elapses
bool displayOverlayPrintf(uint8_t col, uint8_t row, uint64_t timeoutPeriodMs, const char* s, ...);

// Returns true if col and row are within the range
// Display will be updated constantly before the timeout elapses
bool displayOverlayPrint(uint8_t col, uint8_t row, uint64_t timeoutPeriodMs, const char c);