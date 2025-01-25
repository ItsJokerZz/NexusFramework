#pragma once

#include <cstdint>
#include <regex>

#include <sys/socket.h>
#include <dirent.h>
#include <netinet/in.h>
#include <arpa/inet.h> 

#include <orbis/libkernel.h>
#include <orbis/Sysmodule.h>
#include <orbis/SystemService.h>
#include <orbis/UserService.h>
#include <orbis/Http.h>
#include <orbis/Net.h>

#include "nlohmann/json.hpp"
#include <GoldHEN.h>
#include <curl/curl.h>

#include "global_defs.hpp"
#include "server_utils.hpp"
#include "system_utils.hpp"
#include "client_cmds.hpp"
#include "daemon_cmds.hpp"
#include "orbis_control.hpp"
