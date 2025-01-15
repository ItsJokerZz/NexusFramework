#pragma once

namespace System
{
    extern std::string consoleType;

    extern const char *Type();
    extern int32_t GetSystemLanguageID();
    extern const char *GetSystemLanguage();
    extern const char *GetFWVersion();
    extern uint32_t GetCPUTemperature();
    extern uint32_t GetSOCTemperature();

    void TextNotify(int type, const char *_msg);
    void ImageNotify(const char *IconUri, const char *text);
    void SetTemperatureLimit(uint8_t limit);
    void Beep(int type);
}

extern "C"
{
    void sceSysUtilSendSystemNotificationWithText(int type, const char *message);

    int32_t sceSysmoduleLoadModule(OrbisSysModule moduleId),
        sceKernelGetSystemSwVersion(OrbisKernelSwVersion *version);

    uint32_t sceKernelGetCpuTemperature(uint32_t *celsius),
        sceKernelGetSocSensorTemperature(uint32_t *unknown, uint32_t *celsius);
}
