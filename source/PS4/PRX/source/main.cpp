#include "includes.hpp"

/*
 struct proc_prx_load {
   char process_name[32];
   char prx_path[100];
   uint64_t res;
} __attribute__((packed));

struct proc_prx_unload {
   char process_name[32];
   uint64_t prx_handle;
   uint64_t res;
} __attribute__((packed));


uint64_t prx_handle;

int LoadPRXIntoProcess(char *process_name, char *prx_path) {
   struct proc_prx_load args;
   memset(&args, 0, sizeof(struct proc_prx_load));
   strncpy(args.process_name, process_name, sizeof(args.process_name));
   strncpy(args.prx_path, prx_path, sizeof(args.prx_path));

   syscall(500, 6, &args);

   return args.res;
}

int UnloadPRXFromProcess(char *process_name) {
   struct proc_prx_unload args;
   memset(&args, 0, sizeof(struct proc_prx_unload));
   strncpy(args.process_name, process_name, sizeof(args.process_name));
   args.prx_handle = prx_handle;

   syscall(500, 7, &args);

   return args.res;
}
 */

namespace OrbisControl {

}  // namespace OrbisControl

extern "C" void entry() {
  if (loadedFromBIN) 
  OrbisControl::StartServer();
}