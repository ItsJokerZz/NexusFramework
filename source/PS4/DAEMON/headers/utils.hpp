#pragma once

void printMsgToUART(const char *file, const char *func, int line, const char *fmt, ...);
extern char *PerformGETRequest(const char *cmd);
extern bool isRelayRunning();
void HandleCommand(void (*func)(), int socket, bool toggle);
extern char *DecodeURL(const char *url);
void SendResponse(const char *msg, int socket, bool toggle);
void SendFormattedResponse(const char *message, int socket, bool *toggle);
