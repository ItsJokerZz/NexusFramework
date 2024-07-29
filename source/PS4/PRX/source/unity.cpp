#include "unity.hpp"

extern "C" {
namespace Application {
void EnterSandbox() {
  if (freeOfSandbox()) jbc_set_cred(&g_Cred);
}

void BreakFromSandbox() {
  if (freeOfSandbox()) return;
  jbc_get_cred(&g_Cred);
  g_RootCreds = g_Cred;
  jbc_jailbreak_cred(&g_RootCreds);
  jbc_set_cred(&g_RootCreds);
}

void ExitApplication() {
  EnterSandbox();
  sceSystemServiceLoadExec("exit", 0);
}
}

void EnterSandbox();
void BreakFromSandbox();
void ExitApplication();
}