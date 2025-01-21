#pragma once

struct proc_list_entry
{
  char p_comm[32];
  int pid;
} __attribute__((packed));

namespace sys_utils
{
  extern std::string console_type;

  extern int sys_proc_list(struct proc_list_entry *procs, uint64_t *num);
  extern int find_pid_by_procName(const char *proc_name, int *pid);
  extern int find_procName_of_pid(int pid, char *proc_name);

  extern const char *get_username(OrbisUserServiceUserId userId = 0);
  extern const char *get_console_type();
  extern const char *get_fw_version();

  extern uint32_t get_cpu_temperature();
  extern uint32_t get_soc_temperature();

  void text_notify(int type, const char *_msg);
  void image_notify(const char *IconUri, const char *text);
  void set_temperature_limit(uint8_t limit = 60);
  void set_power_state(power_state state = DO_NOTHING);
  void ring_buzzer(int type);

} // namespace sys_utils

extern "C"
{
  void sceSysUtilSendSystemNotificationWithText(int type, const char *message);

  int32_t sceSysmoduleLoadModule(OrbisSysModule moduleId),
      sceKernelGetSystemSwVersion(OrbisKernelSwVersion *version);

  uint32_t sceKernelGetCpuTemperature(uint32_t *celsius),
      sceKernelGetSocSensorTemperature(uint32_t *unknown, uint32_t *celsius);
}