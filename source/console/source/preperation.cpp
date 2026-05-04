#include "headers.hpp"

void log_all_stats();

void sceShellUIUtilInitialize()
{
  int (*_sceShellUIUtilInitialize)() = nullptr;

  const char *path;

#ifdef __PROSPERO__
  path = "/system_ex/common_ex/lib/libSceShellUIUtil.sprx";
#else
  path = "/system/common/lib/libSceShellUIUtil.sprx";
#endif

  int handle = sceKernelLoadStartModule(path, 0, 0, 0, 0, 0);

  sceKernelDlsym(handle, "sceShellUIUtilInitialize",
                 (void **)&_sceShellUIUtilInitialize);

  sceKernelDlsym(handle, "sceShellUIUtilLaunchByUri",
                 (void **)&sceShellUIUtilLaunchByUri);

  _sceShellUIUtilInitialize();
}

#ifdef __ORBIS__
int patch_ptrace()
{ // All credtis go to John Tormblum for this patch. :D
  unsigned char privcaps[16] = {0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff,
                                0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff};
  unsigned int fw = kernel_get_fw_version();
  intptr_t pt_patch = 0; // (req < 0x2b)
  unsigned char caps[16];
  unsigned long jaildir;
  unsigned long rootdir;
  unsigned long prison;
  uint8_t qaflags[16];
  int err;

  switch (fw & 0xffff0000)
  {
  case 0x4000000:
  case 0x4010000:
  case 0x4050000:
  case 0x4060000:
  case 0x4070000:
  case 0x4500000:
  case 0x4550000:
  case 0x4700000:
  case 0x4710000:
  case 0x4720000:
  case 0x4730000:
  case 0x4740000:
    if ((pt_patch = kernel_find_pattern(
             KERNEL_ADDRESS_IMAGE_BASE, KERNEL_IMAGE_SIZE,
             "48b8361000007e020000???????????????????????????????????????????"
             "???????????????????????0f84????????")))
    {
      pt_patch += 43;
    }
    break;
  case 0x5000000:
  case 0x5010000:
  case 0x5030000:
  case 0x5050000:
  case 0x5070000:
  case 0x5500000:
  case 0x5530000:
  case 0x5550000:
  case 0x5560000:
  case 0x6000000:
  case 0x6020000:
  case 0x6200000:
  case 0x6500000:
  case 0x6510000:
  case 0x6700000:
  case 0x6710000:
  case 0x6720000:
    if ((pt_patch = kernel_find_pattern(
             KERNEL_ADDRESS_IMAGE_BASE, KERNEL_IMAGE_SIZE,
             "48b8361000007e020000??????????????????????????"
             "0f84????????")))
    {
      pt_patch += 23;
    }
    break;
  case 0x7000000:
  case 0x7010000:
  case 0x7020000:
  case 0x7500000:
  case 0x7510000:
  case 0x7550000:
  case 0x8000000:
  case 0x8010000:
  case 0x8030000:
  case 0x8500000:
  case 0x8520000:
  case 0x9000000:
  case 0x9030000:
  case 0x9040000:
  case 0x9500000:
  case 0x9510000:
  case 0x9600000:
  case 0x10000000:
  case 0x10010000:
  case 0x10500000:
  case 0x10700000:
  case 0x10710000:
  case 0x11000000:
  case 0x11020000:
  case 0x11500000:
  case 0x11520000:
  case 0x12000000:
  case 0x12020000:
  case 0x12500000:
  case 0x12520000:
  case 0x13000000:
  case 0x13020000:
  case 0x13040000:
  case 0x13500000:
    if ((pt_patch =
             kernel_find_pattern(KERNEL_ADDRESS_IMAGE_BASE, KERNEL_IMAGE_SIZE,
                                 "48b8361000007e020000????????????????????????"
                                 "0f84190200004c8b")))
    {
      pt_patch += 22;
    }
    break;

  default:
    LOG_PRINTF("Unsupported firmware (0x%x)\n", fw);
    return -1;
  }

  if (kernel_get_qaflags(qaflags))
  {
    LOG_PERROR("kernel_get_qaflags");
    return -1;
  }

  qaflags[1] |= 3;
  if (kernel_set_qaflags(qaflags))
  {
    LOG_PERROR("kernel_set_qaflags");
    return -1;
  }

  if (pt_patch)
  {
    LOG_PRINTF("pathing kernel at 0x%lx (ptrace)\n", pt_patch);
    kernel_patch(pt_patch, 0, "\x90\x90\x90\x90\x90\x90", 6);
  }

  return 0;
}
#endif

void initialization()
{
  unlink(LOG_FILE);

  sceKernelSetProcessName(NAME);

  sceNetCtlInit();            /* fucks up username ??? */
  sceShellUIUtilInitialize(); /* fucks up username ??? */

  user_service_init_params userServiceParams{};
  userServiceParams.priority = KERNEL_PRIO_FIFO_HIGHEST;
  sceUserServiceInitialize(&userServiceParams);

#ifdef __ORBIS__
  patch_ptrace();
#endif

  g_system_info.summary();
  log_all_stats();

  pid_t current_pid = getpid();
  for (const auto &proc : get_proc_list())
  {
    if (proc.exec == NAME && proc.process_id != current_pid)
    {
      log_message("Killing duplicate process PID: %d", proc.process_id);
      kill(proc.process_id, SIGKILL);
    }
  }
  log_message("Successfully started! (PID: %d)", current_pid);
}

void create_threads()
{
  pthread_t thread;

  pthread_create(&server.threads.server, nullptr, threads::API, nullptr);
  pthread_detach(server.threads.server);
}

void wait_for_unload()
{
  notify_debug(NAME " has loaded!");

  while (!unloaded)
    sleep(1);

  raise(SIGKILL);
}

int main(void)
{
  initialization();
  create_threads();
  wait_for_unload();

  return EXIT_FAILURE;
}