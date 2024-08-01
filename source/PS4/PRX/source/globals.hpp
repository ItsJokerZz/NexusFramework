#include "includes.hpp"

extern jbc_cred 
g_Cred, g_RootCreds;

extern uint16_t 
inputTextBuffer[512 + 1],
input_ime_title[512];

extern bool freeOfSandbox();

extern bool Keyboard(const char* Title, const char* initialTextBuffer, char* out_buffer);

extern "C" {
int32_t
    sceSysmoduleLoadModule(OrbisSysModule moduleId),
    sceKernelGetSystemSwVersion(OrbisKernelSwVersion* version);

uint32_t
    sceKernelGetCpuTemperature(uint32_t* celsius),
    sceKernelGetSocSensorTemperature(uint32_t* unk, uint32_t* celsius);
}