#pragma once

#include <string>
#include <cstdint>
#include <memory>

namespace sys_utils
{
    extern std::string console_type;

    extern int sys_proc_list(struct proc_list_entry *procs, uint64_t *num);
    extern int find_pid_by_name(const char *proc_name, int *pid);
    extern int find_name_of_pid(int pid, char *proc_name);

    extern const char *get_username(OrbisUserServiceUserId = 0);
    extern const char *get_console_type();
    extern int32_t get_system_language_id();
    extern const char *get_system_language();
    extern const char *get_fw_version();
    extern uint32_t get_cpu_temperature();
    extern uint32_t get_soc_temperature();

    void text_notify(int type, const char *_msg);
    void image_notify(const char *IconUri, const char *text);
    void set_temperature_limit(uint8_t limit = 60);
    void ring_buzzer(int type);
}

extern "C"
{
    void sceSysUtilSendSystemNotificationWithText(int type, const char *message);

    int32_t sceSysmoduleLoadModule(OrbisSysModule moduleId),
        sceKernelGetSystemSwVersion(OrbisKernelSwVersion *version);

    uint32_t sceKernelGetCpuTemperature(uint32_t *celsius),
        sceKernelGetSocSensorTemperature(uint32_t *unknown, uint32_t *celsius);
}
