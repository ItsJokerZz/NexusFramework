#pragma once

// Standard Libraries
#include <algorithm>
#include <chrono>
#include <cstdarg>
#include <cstdio>
#include <cstdlib>
#include <ctime>
#include <iostream>
#include <string>
#include <unordered_map>
#include <utility>
#include <vector>

// System Libraries
#include <sys/socket.h>

// Orbis SDK
#include <orbis/Http.h>
#include <orbis/Net.h>
#include <orbis/Sysmodule.h>
#include <orbis/SystemService.h>
#include <orbis/UserService.h>
#include <orbis/libkernel.h>

// External Libraries
#include <GoldHEN.h>

// JSON Library
#include "nlohmann/json.hpp"

// Project-specific Includes
#include "client_cmds.hpp"
#include "daemon_cmds.hpp"
#include "global_defs.hpp"
#include "orbis_control.hpp"
#include "server_utils.hpp"
#include "system_utils.hpp"