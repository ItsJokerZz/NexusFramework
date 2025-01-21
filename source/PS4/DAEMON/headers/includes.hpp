#pragma once

// Standard Libraries
#include <iostream>
#include <string>
#include <chrono>
#include <ctime>
#include <cstdarg>
#include <cstdio>
#include <cstdlib>
#include <vector>
#include <utility>
#include <algorithm>
#include <unordered_map>

// System Libraries
#include <sys/socket.h>

// Orbis SDK
#include <orbis/Net.h>
#include <orbis/Http.h>
#include <orbis/libkernel.h>
#include <orbis/UserService.h>
#include <orbis/SystemService.h>
#include <orbis/Sysmodule.h>

// External Libraries
#include <GoldHEN.h>

// JSON Library
#include "nlohmann/json.hpp"

// Project-specific Includes
#include "definitions.hpp"
#include "server_utils.hpp"
#include "system_utils.hpp"
#include "client_cmds.hpp"
#include "daemon_cmds.hpp"