#pragma once

struct proc_list_entry
{
  char p_comm[32];
  int pid;
} __attribute__((packed));

struct free_and_alloc_args
{
  uint64_t address;
  uint64_t length;
} __attribute__((packed));

enum power_state
{
  DO_NOTHING = -1,
  POWER_OFF = 31,
  RESTART = 30,
  RESTMODE = 1,
};

extern std::string console_type;
extern std::string get_local_ip();

extern std::string get_apps_titleid();
extern std::string get_apps_name();
extern std::string get_apps_version();
extern std::string get_apps_minFW();
extern std::string get_apps_region();
extern std::string get_app_info(const std::string &returnType);
extern std::string parse_apps_sfo_param(const std::string &key);

extern std::string find_exec_by_titleID();
extern std::string find_procName_of_pid(int pid);

extern int find_pid_by_procName(const char *proc_name);
extern int get_proc_list(struct proc_list_entry *procs, uint64_t *num);
extern int sys_proc_cmd(uint64_t pid, uint64_t cmd, void *data);
extern int sys_proc_alloc(uint64_t pid, free_and_alloc_args *args, bool free = false);

extern const char *get_username(OrbisUserServiceUserId userId = 0);

extern const char *get_console_type();
extern const char *get_fw_version();

extern uint32_t get_cpu_temperature();
extern uint32_t get_soc_temperature();

extern bool has_entered_restmode();

void text_notify(int type, const char *_msg);
void image_notify(const char *IconUri, const char *text);

void set_temperature_limit(uint8_t limit = 60);
void set_power_state(power_state state = DO_NOTHING);
void ring_buzzer(int type);

extern "C"
{
  void sceSysUtilSendSystemNotificationWithText(int type, const char *message);

  int32_t sceSysmoduleLoadModule(OrbisSysModule moduleId),
      sceKernelGetSystemSwVersion(OrbisKernelSwVersion *version);

  uint32_t sceKernelGetCpuTemperature(uint32_t *celsius),
      sceKernelGetSocSensorTemperature(uint32_t *unknown, uint32_t *celsius);
}
