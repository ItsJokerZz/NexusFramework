#include "includes.hpp"

extern "C" void entry() {
  if (loadedFromBIN) 
  OrbisControl::StartServer();
}