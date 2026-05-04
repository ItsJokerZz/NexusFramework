#pragma once

#ifdef __PROSPERO__
#define SHELLUI_TID "NPXS40087"
#else
#define SHELLUI_TID "NPSX20001"
#endif

struct process_info {
  int app_id;
  std::string tid;
  std::string exec;
  pid_t process_id;
};

std::vector<process_info> get_proc_list();

template <typename F> process_info find_in_proc_list(F &&predicate) {
  auto list = get_proc_list();

  if (list.empty())
    return {};

  process_info best_match{};

  for (const auto &p : list) {
    if (p.process_id <= 0)
      continue;

    if (predicate(p)) {
      if (p.exec == "SceShellUI" || p.exec == "eboot.bin")
        return p;

      if (best_match.process_id <= 0)
        best_match = p;
    }
  }

  return best_match;
}

template <typename V, typename M>
bool is_match(const process_info &p, V v, M m) {
  return p.*m == v;
}

inline bool is_match(const process_info &p, const char *v,
                     std::string process_info::*m) {
  return v && (p.*m == v);
}

template <typename Key, typename KeyMem, typename RetMem, typename Default>
auto get_proc_info(Key key, KeyMem key_mem, RetMem ret_mem, Default def) {
  auto p = find_in_proc_list(
      [&](const auto &proc) { return is_match(proc, key, key_mem); });

  using RetT = std::remove_reference_t<decltype(p.*ret_mem)>;

  if (p.process_id > 0)
    return static_cast<RetT>(p.*ret_mem);

  return static_cast<RetT>(def);
}

pid_t find_pid_by_exec(const char *name);

int get_running_app_id();
std::string get_running_app_tid();
std::string get_running_app_exec();
pid_t get_running_app_pid();
std::string get_running_app_exec();
std::string get_name_by_tid(std::string_view tid);
std::string get_app_version();
std::string get_app_sdk();
std::string get_app_region();
bool is_daemon_process();
