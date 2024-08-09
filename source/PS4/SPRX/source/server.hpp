#include "includes.hpp"
extern float version;

extern bool loadedFromBIN;
extern bool breakThread;

extern char buffer[BUFFER_SIZE];
extern char response[BUFFER_SIZE];

extern bool connected;
extern std::string consoleType;

namespace OrbisControl {
extern int server_sock, client_sock;

extern int HandlePlugin(int load, ...);

extern char* DecodeURL(const char* url);
extern void SendResponse(const char* message);

namespace CMDS {
    extern void Version();

    extern void Connect();
    extern void Disconnect();
    extern void Unload();

    extern void GetFW();
    extern void GetTemp();
    extern void SysType();

    extern void Notify();
    extern void Beep();
    extern void TempLimit();
    
    extern void LoadSPRX();
    extern void UnloadSPRX();
}

extern void HandleCommand(void (*func)());
extern void* HandleClients(void* arg);
extern void StartServer();
}