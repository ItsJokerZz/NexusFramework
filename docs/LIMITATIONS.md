# Limitations (be honest)

## Build / validation
- ✅ **Build-verified.** v0.2 builds with `dotnet build -c Release` (0 errors, 0 warnings).
- ✅ **Test-verified.** 50 unit tests pass (`dotnet test -c Release`).
- The .NET 10 SDK was used for local validation; CI targets .NET 8.0 for broader compatibility.

## Functional gaps
- **No payload-side AOB scan.** Scanning runs over `read_memory` HTTP. On
  large process images this is much slower than a payload-local memcmp.
  `INexusClient.AobScanAsync` defines the contract for a future payload-side
  endpoint, but the native payload does not implement it yet.
- **No payload-side pad polling.** `ShortcutDetector` is correct, and
  `PadStatePollingService` provides the host-side polling loop, but the
  native payload does not expose `scePadReadState`. `GetPadStateAsync` is a
  contract-only method on `INexusClient`.
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
  a planned feature.

## Cheat application
- **Freeze loops work** for absolute addresses. For freeze on module/AOB/pointer-resolved
  addresses, use a separate `write_value` or `pointer_write_value` code that writes the
  same value repeatedly from a scheduler loop. The `freeze_value` code type resolves
  `address` as an absolute address; to freeze at a dynamically resolved address, combine
  `aob_write_value` with a scheduler loop.
- **No shellcode/detour cheats** — the Nexus primitives exist (`InstallShellcode`,
  `InjectStartDetour`, `RemoteCallMethod`) but wiring them into the cheat code-type
  catalog is documented future work disabled by default behind
  `allowAdvancedCodeExecution`.
- **Session survival** — if the game closes, in-memory patch records are lost.
  Re-enable on relaunch. Auto-reapply is not implemented.

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
- This project ships as a top-level directory in `thatboialex/NexusFramework`
  for historical reasons. To migrate to a standalone repo, clone the current
  directory, remove unrelated upstream files, and push to a fresh
  `NexusCheatFramework` repository. There are no path-relative dependencies
  that block migration.
