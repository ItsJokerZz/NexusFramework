#include "../headers/includes.hpp"

std::array<ErrorMessage, ERROR_COUNT> error_messages = {{
    {"DEBUGGING ERROR"},
    {"Server is running, but no command was passed. Please check."},
    {"Command not found. Please check and try again."},
    {"Not connected to the server. Check your connection."},
    {"Not attached to process. Open an app and attach."},
    {"An unknown error occurred. Please try again later."},
    {"Invalid parameters provided. Please verify and retry."},
}};

bool DEBUG = true,
     isDaemon = false,
     unloaded = false,
     connected = false,
     attached = false;

serverData data;

uint16_t port = UINT16_MAX;
std::string name = "";