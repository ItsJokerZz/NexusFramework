#include "includes.hpp"

extern "C" {
namespace Application {
   extern void EnterSandbox();
   extern void BreakFromSandbox();
   extern void ExitApplication();
}

extern void EnterSandbox();
extern void BreakFromSandbox();
extern void ExitApplication();
}