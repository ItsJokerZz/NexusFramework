#pragma once

/* ============================
   Standard C Headers
   ============================ */
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

/* ============================
   C++ Standard Library Headers
   ============================ */
#ifdef __cplusplus
#include <algorithm>
#include <csignal>
#include <cstdarg>
#include <cstdio>
#include <filesystem>
#include <fstream>
#include <inttypes.h>
#include <mutex>
#include <regex>
#include <sstream>
#include <string>
#include <vector>
#endif

/* ============================
   POSIX / System Headers
   ============================ */
#include <dirent.h>
#include <fcntl.h>
#include <unistd.h>

#include <arpa/inet.h>

#include <netinet/in.h>
#include <netinet/tcp.h>

#include <sys/ioctl.h>
#include <sys/mman.h>
#include <sys/mount.h>
#include <sys/proc.h>
#include <sys/ptrace.h>
#include <sys/resource.h>
#include <sys/socket.h>
#include <sys/stat.h>
#include <sys/syscall.h>
#include <sys/sysctl.h>
#include <sys/types.h>
#include <sys/uio.h>
#include <sys/user.h>
#include <sys/wait.h>

/* ============================
   Platform / Payload SDK
   ============================ */
#ifdef __PROSPERO__
#include <ps5/kernel.h>
#include <ps5/klog.h>
#include <ps5/mdbg.h>
#include <ps5/nid.h>
#else
#include <ps4/kernel.h>
#include <ps4/klog.h>
#include <ps4/mdbg.h>
#include <ps4/nid.h>
#endif

/* ============================
   External Libraries
   ============================ */
#include "elfldr.hpp"
#include "hde64.hpp"
#include "nineS.hpp"
#include "nlohmann_json.hpp"
#include "ptrace.hpp"
#include "sfo_parser.hpp"

/* ============================
   Project Headers
   ============================ */
#include "api_commands.hpp"
#include "global_defs.hpp"
#include "memory_utils.hpp"
#include "process_utils.hpp"
#include "sdk_defs.hpp"
#include "server_threads.hpp"
#include "server_utils.hpp"
#include "system_utils.hpp"

/* ============================
   C++ Aliases / Namespaces
   ============================ */
#ifdef __cplusplus
using json = nlohmann::json;
namespace fs = std::filesystem;
using namespace cmds;
#endif