#pragma once

#include "includes.hpp"

namespace System
{
    extern std::string consoleType;

    void TextNotify(int type, const char *_msg);
    void ImageNotify(const char *IconUri, const char *text);
    const char *Type();
    int32_t GetSystemLanguageID();
    const char *GetSystemLanguage();
    const char *GetFWVersion();
    uint32_t GetCPUTemperature();
    uint32_t GetSOCTemperature();
    void SetTemperatureLimit(uint8_t limit);
    void Beep(int type);
}
