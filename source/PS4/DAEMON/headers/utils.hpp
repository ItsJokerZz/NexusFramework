#pragma once

void PrintMsgToUART(const char *fmt, ...);
extern char *PerformGETRequest(const char *cmd);
void HandleCommand(void (*func)(), int socket, bool toggle);
extern char *DecodeURL(const char *url);
void SendFormattedResponse(const char* message, int socket, bool* toggle);
void SendResponse(const char *msg, int socket, bool toggle);
