#include "includes.hpp"

extern "C" {
namespace System {
extern void PrintToConsole(const char* message, int type);
extern void Notify(int type, const char* _msg);
extern const char* Type() ;
extern void MountRootDirectories();
extern int32_t GetSystemLanguageID();
extern const char* GetSystemLanguage();
extern const char* GetFWVersion();
extern uint32_t GetTemperature();
extern void SetTemperatureLimit(uint8_t limit);
extern const char* GetKeyboardInput(const char* title, const char* initialText);
extern void Beep(int type);
}

extern void PrintToConsole(const char* message, int type);
extern void Notify(int type, const char* _msg);
extern const char* Type() ;
extern void MountRootDirectories();
extern int32_t GetSystemLanguageID();
extern const char* GetSystemLanguage();
extern const char* GetFWVersion();
extern uint32_t GetTemperature();
extern void SetTemperatureLimit(uint8_t limit);
extern const char* GetKeyboardInput(const char* title, const char* initialText);
extern void Beep(int type);
}