#include "includes.hpp"

extern "C" {
namespace System {
extern void PrintToConsole(const char* message, int type);
extern void TextNotify(int type, const char* _msg);
extern void ImageNotify(const char* IconUri, const char* text);
extern const char* Type();
extern void MountRootDirectories();
extern int32_t GetSystemLanguageID();
extern const char* GetSystemLanguage();
extern const char* GetFWVersion();
extern uint32_t GetCPUTemperature();
extern uint32_t GetSOCTemperature();
extern void SetTemperatureLimit(uint8_t limit);
extern const char* GetKeyboardInput(const char* title, const char* initialText);
extern void Beep(int type);
}

extern void PrintToConsole(const char* message, int type);
extern void TextNotify(int type, const char* _msg);
extern void ImageNotify(const char* IconUri, const char* text);
extern const char* Type();
extern void MountRootDirectories();
extern int32_t GetSystemLanguageID();
extern const char* GetSystemLanguage();
extern const char* GetFWVersion();
extern uint32_t GetCPUTemperature();
extern uint32_t GetSOCTemperature();
extern void SetTemperatureLimit(uint8_t limit);
extern const char* GetKeyboardInput(const char* title, const char* initialText);
extern void Beep(int type);
}