# Limitations (be honest)

## Build / validation
- This drop was assembled in an environment with no .NET SDK installed.
  Source is reviewed by hand and unit tests are written, but the project
  has not been built end-to-end by the author. Trust CI, not the author.

## Functional gaps
- **No payload-side AOB scan.** Scanning runs over `read_memory` HTTP. On
  large process images this is much slower than a payload-local memcmp.
- **No payload-side pad polling.** `ShortcutDetector` is correct, but
  there's currently no Nexus endpoint that returns `scePadReadState`. To
  drive shortcuts on-console you must add that endpoint or run from a
  host overlay app.
- **No in-game overlay rendering.** The cheat menu is delivered through
  a CLI / WebUI companion plus an etaHEN toolbox link, not as a native
  overlay drawn over the running game.
- **No Share / Create button capture from outside shellui.** On retail PS4
  / PS5 the system intercepts Share before games see it; the
  `LongHoldShare` / `SingleTapShare` modes therefore depend on
  shellui-side cooperation in production.

## Cheat formats
- Only the native JSON format is implemented end-to-end.
- `EtaHenCheatParser`, `ShnCheatParser`, `Mc4CheatParser` are stubs that
  throw with messages because none of the three formats has a public,
  documented spec in the bundled upstream sources.
- `enabledByDefault` is parsed but not auto-applied on connect; that's
  on the v0.2 list.

## Cheat application
- Only in-process memory writes. No shellcode/detour cheats yet — the
  Nexus primitives exist (`InstallShellcode`, `InjectStartDetour`,
  `RemoteCallMethod`) but wiring them into the cheat code-type catalog
  is future work.
- No freeze loops yet. They can be layered on `WriteValue` with a host
  timer; an explicit `freeze_value` type is planned.
- No pointer-chain cheats.
- The engine cannot survive a process exit. If the game closes, the
  in-memory record of "what was patched" disappears. Re-enable on
  relaunch.

## Online / multiplayer
- Out of scope and explicitly unsupported. See
  `docs/SECURITY_AND_ETHICS.md`.

## Hardware / firmware
- Tested manually only against the Nexus payload's documented surface.
  No firmware-specific quirks are handled here; all firmware variation is
  inherited from NexusFramework.
- `MemoryProtection` flags as reported by `get_vm_maps` occasionally come
  back as 0 on some PS5 firmware. The scanner skips those by default
  (`ScanReadableOnly = true`). Use `RegionNameContains` to whitelist
  modules of interest if you must scan zero-prot regions.

## Repo layout
- This release ships as a directory inside `thatboialex/nexusframework`
  rather than its own top-level repository. The MCP scope only allows
  pushing to that repo. Move it to a fresh `NexusCheatFramework` repo
  when convenient — there are no path-relative dependencies that block
  that.
