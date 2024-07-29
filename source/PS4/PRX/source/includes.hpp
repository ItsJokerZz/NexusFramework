#pragma once

// Standard Libraries
#include <string>
#include <cstdarg>
#include <stdio.h>
#include <stdint.h>
#include <wchar.h>

// File and Socket Operations
#include <fcntl.h>
#include <unistd.h>
#include <sys/uio.h>
#include <sys/stat.h>
#include <sys/socket.h>

// Orbis SDK Libraries
#include <orbis/Net.h>
#include <orbis/libkernel.h>
#include <orbis/Sysmodule.h>
#include <orbis/ImeDialog.h>
#include <orbis/UserService.h>
#include <orbis/SystemService.h>
#include <orbis/SysUtil.h>

// Other Libraries
#include <libjbc.h>

// Project Dependencies
#include "defines.hpp"
#include "globals.hpp"
#include "utilities.hpp"

#include "unity.hpp"
#include "system.hpp"
#include "server.hpp"