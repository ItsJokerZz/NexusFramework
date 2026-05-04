# Attribution & Upstream Inventory

## NexusFramework
- Repository: https://github.com/ItsJokerZz/NexusFramework
- License: MIT (per the upstream `Copyright` field on
  `NexusFramework.csproj`).
- What we reuse:
  - The HTTP API contract used by the payload (read_memory, write_memory,
    get_vm_maps, get_proc_list, get_proc_info, memory_protection,
    connect/disconnect, version, status).
  - The `MemoryProtection` flag layout (Read/Write/Execute/Copy bits).
  - Conceptual data model (Target/Process/MemoryEntry).
- How we reuse it:
  - The `INexusClient` interface mirrors only the surface we need.
  - `HttpNexusClient` is a clean reimplementation against the same HTTP
    endpoints; it does not link the upstream assembly.
  - `NexusCheatFramework.NexusAdapter` is an optional project that wraps the
    upstream `NexusFramework.Library`. It is **not** part of the default
    solution build.
  - The upstream `ArrayOfBytesScan` placeholder (commented as "Not yet
    implemented on the API/payload (backend)") is replaced by our own
    `AobScanner`; no upstream scanner code is copied because there isn't a
    finished one to copy.
- License compatibility:
  - MIT is GPL-compatible. Our wrapper / reimplementation can ship under
    GPLv3 along with the rest of this repo.

## etaHEN
- Repository: https://github.com/etaHEN/etaHEN
- License: GPLv3 (per the bundled toolbox / shellui / daemon sources).
- What we reuse — **patterns and behavior, not source**:
  - The shellui toolbox XML structure for adding a Cheats page (see
    `ETAHEN/shellui/assets/etaHEN_toolbox.xml`). Our `nexus_cheats.xml` is a
    new XML written to the same shellui schema.
  - The four cheats-shortcut presets surfaced in
    `ETAHEN/shellui/include/HookedFuncs.hpp`'s `Cheats_Shortcut` enum
    (R3+L3, L2+△, long Options, long Share, single Share).
  - The pad button bit-layout, which is itself a public Sony / OpenOrbis
    layout — the values in `Input/PadButton.cs` mirror the same constants
    found in `ETAHEN/daemon/include/globalconf.hpp` and any open-source
    PS4/PS5 SDK.
- What we did **not** copy:
  - No etaHEN C/C++ source files were copied.
  - The `ShortcutDetector` is a clean reimplementation of the publicly
    visible behavior (hold + long-press + tap + debounce). We did not lift
    the implementation in `ETAHEN/shellui/src/HookFunctions.cpp`.
  - etaHEN's "Cheats (WIP)" engine, daemon `libhijacker_cheats` switch, and
    Illusion-cheats integration are **not** ported. We surface them as
    related work, but the live cheat path here goes through Nexus, not
    libhijacker.
- License compatibility:
  - To stay safe given the design influence and shared UX vocabulary, this
    project uses GPL-3.0-or-later. If you later determine that no
    GPL-derived material is present and want to relicense to MIT, audit
    `Input/ShortcutDetector.cs`, `Input/ShortcutConfig.cs`, and the menu
    XML files first.

## Cheat Manager Shortcut
- The cheat-manager-shortcut UX is behaviorally inspired by etaHEN's
  `Cheats_shortcut_opt` toolbox flow. No etaHEN source code was used; only
  the user-facing behavior was referenced.

## Other upstreams referenced for context
- PS5 Payload Dev SDK — used by both NexusFramework and etaHEN payloads;
  not consumed at the C# layer of this project.
- libhijacker (https://github.com/astrelsky/libhijacker) — etaHEN bundles
  it; not used here.
- elfldr (John Törnblom) — referenced by NexusFramework as a payload
  injector. Not consumed at the C# layer.
- PS5Debug (Sistr0 / CTN) — referenced as an etaHEN-optional plugin; not
  consumed.

## Files in this repo derived from / inspired by the above
- `menu/etaHEN_xml/nexus_cheats.xml` — new XML, etaHEN shellui schema.
- `menu/etaHEN_xml/toolbox_link.snippet.xml` — single line referencing the
  etaHEN toolbox layout.
- `src/NexusCheatFramework.Core/Input/PadButton.cs` — value constants
  shared with etaHEN/Sony/OpenOrbis SDKs.
- `src/NexusCheatFramework.Core/Input/ShortcutConfig.cs` — enum mirrors
  etaHEN's user-facing names so existing users recognize the options.
- `src/NexusCheatFramework.Core/Nexus/HttpNexusClient.cs` — uses the
  NexusFramework payload's HTTP endpoint names.
- `src/NexusCheatFramework.NexusAdapter/NexusClientAdapter.cs` — thin
  wrapper around the upstream NexusFramework C# library.

If you spot a derivation that needs stronger attribution, please open an
issue.
