# Upstream Analysis

This is the inspection record produced before any code in this repo was
written. Line numbers reference the snapshots bundled in `ETAHEN/` and
`NexusFramework/` at the root of the same git repo; verify against
upstream HEAD before relying on them.

## NexusFramework

### C# library (the dependency we adapt)
- `NexusFramework/source/libraries/C#/library.cs` — entrypoint, version
  reporting, target/process state holders.
- `NexusFramework/source/libraries/C#/definitions.cs` — `MemoryEntry`,
  `MemoryProtection` flags (Read=1, Write=2, Exec=4, Copy=8),
  `ProcessInfo`, `LoadedELF/Module/Shellcode`, `HookAttribute`.
- `NexusFramework/source/libraries/C#/commands/connection.cs` — REST verbs
  used: `status`, `version`, `connect`, `disconnect`, `unload`,
  `get_sys_info`, `get_proc_list`, `get_proc_info`. Notable: the upstream
  skips `GetVirtualMemoryMaps` when the active TID is one of
  `Definitions.ShellUI_TIDs` (`NPXS40087`, `NPSX20001`).
- `NexusFramework/source/libraries/C#/commands/process.cs` — memory APIs
  we mirror: `AllocateMemory`, `FreeMemory`, `ReadMemory<T>`, `WriteMemory<T>`,
  `LoadELF`, `UnloadELF`, `GetVirtualMemoryMaps`, `SetMemoryProtection`,
  `LoadModule`, `UnloadModule`, `GetModuleHandle`, `ResolveSymbol`,
  `InstallShellcode`, `RemoveShellcode`, `RemoteCallMethod`,
  `InjectStartDetour`, `SuspendProcess`, `ResumeProcess`.
- **`ArrayOfBytesScan` (process.cs:363)** — comment: `// Not yet implemented
  on the API/payload (backend)`. Client-side scan exists but only returns a
  single match, has no region filter, and has a hard-coded chunk size of
  `0x40000`. Replaced by our own `AobScanner`.
- HTTP details we copy verbatim: query strings on `read_memory`, JSON body
  on `write_memory` with `data` as lowercase hex, `prot` enum cast to uint
  on `memory_protection`.

### Payload / native side
- Headers: `NexusFramework/source/console/headers/{api_commands,memory_utils,
  process_utils,server_utils,server_threads,sdk_defs,system_utils}.hpp`.
- Implementation: matching `.cpp` files under
  `NexusFramework/source/console/source/`.
- The payload exposes one HTTP server multiplexing the verbs above; no
  dedicated AOB endpoint exists today (consistent with the C# comment).
- `ShellUI_TIDs` (`NPXS40087`, `NPSX20001`) is the system shell, which has
  no useful VM map to scan; we keep the same exclusion behavior in
  `CheatManager`.

### What we reuse vs. reimplement
| | Reuse | Reimplement | Notes |
|---|---|---|---|
| HTTP endpoint shapes | ✓ | | mirrored byte-for-byte in `HttpNexusClient` |
| `MemoryProtection` flag values | ✓ | | identical numeric values |
| AOB scanning | | ✓ | upstream is a stub; we wrote the real thing |
| ELF/shellcode loading | | (out of scope for v0.1) | tracked for v0.2 |
| `RemoteCallMethod` / detours | | (not needed yet) | revisit when we add detour-style cheats |

### License implications (Nexus)
MIT; freely combinable with GPLv3. We chose to wrap (not fork) the upstream
client and to keep the wrapper opt-in (`NexusCheatFramework.NexusAdapter`).

## etaHEN

### Files inspected
- `ETAHEN/README.md` — documents `libhijacker_cheats` and
  `Cheats_shortcut_opt` config keys.
- `ETAHEN/daemon/include/globalconf.hpp` —
  `bool libhijacker_cheats = false;` (line 29) plus the
  `ScePadButtonDataOffset` enum (lines 544–562) and `ScePadData` struct
  (lines 655–683).
- `ETAHEN/daemon/source/msg.cpp:394–417` — INI parsing for
  `Settings.libhijacker_cheats`.
- `ETAHEN/shellui/include/HookedFuncs.hpp:158-188` — the four shortcut
  enums (`Cheats_Shortcut`, `Toolbox_Shortcut`, `Games_Shortcut`,
  `Kstuff_Shortcut`) and the `etaHENSettings_t` struct that holds them.
- `ETAHEN/shellui/src/HookFunctions.cpp:2600-2700+` — per-shortcut hold
  detection state, `LONG_PRESS_DURATION`, `cheas_sc_activated` flag, and
  the URI-handler call `GoToURI("etaHEN?Cheats")` that opens the menu.
- `ETAHEN/shellui/src/MonoUtils.cpp:634-642 / 716 / 749-752` — INI ↔
  struct round-trip for the four shortcut options.
- `ETAHEN/shellui/assets/etaHEN_toolbox.xml`:
  - line 15: `<link id="id_cheats" title="Cheats (WIP)" file="cheats.xml"/>`
  - lines 112-145: the `<setting_list id="id_shortcuts" ...>` block listing
    each shortcut option as `<list_item value="N"/>`.

### What can be reused directly
- The shellui XML *schema* (it's a documented Sony format used by the
  toolbox plugin). Drop-in compatible if we follow the same elements.

### What must be ported (not copied)
- The Cheats menu UX → modeled by our `CheatMenuModel` + WebUI + XML link.
- Controller shortcut behavior → reimplemented as a deterministic state
  machine in `Input/ShortcutDetector.cs`. We only adopted the user-facing
  preset names so existing etaHEN users recognize the choices.

### What must be reimplemented from scratch
- The actual cheat application path. etaHEN's `libhijacker_cheats` plumbing
  hands cheats off to libhijacker; we instead route through Nexus's
  read/write/AOB primitives.
- The cheat-file format. etaHEN does not ship a documented format — only a
  WIP menu — so we authored a native JSON format (`docs/CHEAT_FORMATS.md`).

### Unimplemented upstream APIs that block features
- **No payload-side AOB scan** in NexusFramework → we scan client-side.
  Faster server-side scanning is tracked as a future endpoint; see
  `docs/NEXUS_API_ENDPOINTS.md`.
- **No payload-side `scePadReadState` exposure** in NexusFramework → live
  controller polling can't run on-console without payload work. The
  detector still runs deterministically against any pad-state source
  (overlay app, host-side dualsense, etc.).
- **No notification/menu-open API** in NexusFramework → triggering an
  in-game overlay from a shortcut requires either (a) etaHEN's `GoToURI`
  inside shellui (GPLv3) or (b) a new Nexus payload endpoint that posts an
  on-screen toast. Today we surface the menu via the WebUI/CLI companion.

### License implications (etaHEN)
GPL-3.0; combining with our own additions forces the combined work to
GPL-3.0-or-later. We deliberately did *not* lift any C++ from etaHEN to
keep the line of derivation narrow and the legal status simple.

## Technical risks
- The AOB scanner reads through Nexus HTTP, which is several orders of
  magnitude slower than payload-local memcmp. Large-region scans on PS5
  can take seconds. Mitigations: region filters, chunk size tuning,
  cancellation tokens, tight pre-anchored patterns, and a future
  `aob_scan` payload endpoint.
- Memory protection flags returned by `get_vm_maps` on PS5 firmware vary;
  our scanner defaults to readable-only, but rare regions report
  `PROT=0` and are skipped. Use `--strict` if you want failures rather
  than skips.
- Restoring original bytes after a process exit is impossible; the engine
  forgets enabled state on disconnect. Document this in
  `docs/LIMITATIONS.md`.
