#pragma once

#include <iostream>
#include <unordered_map>
#include <string>
#include <chrono>
#include <ctime>
#include <cstdarg>
#include <cstdio>
#include <cstdlib>
#include <vector>
#include <utility>
#include <algorithm>

#include <sys/socket.h>

#include <orbis/Net.h>
#include <orbis/Http.h>
#include <orbis/libkernel.h>
#include <orbis/SystemService.h>
#include <orbis/Sysmodule.h>

#include <libjbc.h>
#include <GoldHEN.h>

#include "definitions.hpp"
#include "server_utils.hpp"
#include "system_utils.hpp"

#include "client_cmds.hpp"
#include "daemon_cmds.hpp"

#include "daemon_server.hpp"
#include "relay_server.hpp"
