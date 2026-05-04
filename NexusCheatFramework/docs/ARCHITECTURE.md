# Architecture

```
+--------------------- caller (CLI / WebUI / your app) ---------------------+
|                                                                           |
|  CheatManager  ────────────►  CheatEngine  ──────────►  CheatRuntimeOpts  |
|       │                              │                                    |
|       │ loads                        │ resolves codes via                 |
|       ▼                              ▼                                    |
|  CheatDatabaseService          AobScanner                                 |
|       │                              │                                    |
|       │ uses                         │ chunked reads                      |
|       ▼                              ▼                                    |
|  ICheatFormatParser ──── INexusClient (interface) ────► HttpNexusClient   |
|     (Json,                           ▲                  NexusClientAdapter|
|      EtaHen stub,                    │                                    |
|      Shn stub,                       │                                    |
|      Mc4 stub)                       │                                    |
|                                      │                                    |
+──────────────────────────────────────┼────────────────────────────────────+
                                       │  HTTP (NexusFramework payload :9090)
                                       ▼
+──────── PS4 / PS5 (Nexus payload) ────────+
|  read_memory / write_memory / get_vm_maps |
|  get_proc_info / memory_protection / …    |
+───────────────────────────────────────────+
```

## Layers

### `Nexus` — transport / abstraction
- `INexusClient` defines the surface every other layer uses (pure async,
  cancellation-aware, no static state).
- `HttpNexusClient` talks directly to the Nexus payload's HTTP API. Use
  this when you want zero coupling to the upstream C# library.
- `NexusClientAdapter` (separate optional project) wraps the upstream
  `NexusFramework.Library` for callers that already use it.
- All addresses are `ulong`, all reads return raw `byte[]` so higher
  layers can decide on encoding.

### `Memory` — pattern + patch primitives
- `AobPattern` parses textual patterns, normalizes wildcards, and keeps
  a precomputed first-solid byte for fast chunk skipping.
- `AobScanner` walks regions returned by `INexusClient.GetVirtualMemoryMapsAsync`,
  reading `(ChunkSize + pattern.Length-1)`-byte windows so cross-boundary
  matches are caught.
- `BufferAobSearch` is the in-memory equivalent for tests and callers that
  already have bytes in hand.
- `MemoryPatch` is a value object; the engine owns lifetime.

### `Formats` — cheat file model + parsers
- `CheatFile` / `CheatDefinition` / `CheatCode` are pure DTOs.
- `JsonCheatParser` is the supported, documented parser.
- `EtaHenCheatParser`, `ShnCheatParser`, `Mc4CheatParser` are stubs that
  throw with helpful messages until those formats are documented.

### `Core` — orchestration
- `CheatEngine` owns enable/disable for a single connection. It resolves
  each `CheatCode` to `(address, patchBytes)`, reads originals, optionally
  validates against `expectedBytes`, and tracks applied patches per cheat
  ID. Disable restores originals (configurable).
- `CheatManager` couples the engine to a database and an active process.
  It loads cheat files, filters by Title ID, and exposes the list to the
  UI layer.
- `CheatRuntimeOptions` controls safety / dry-run / ambiguous-match
  policy.

### `Input` — controller shortcuts
- `PadButton` is a flag enum mirroring Sony / OpenOrbis pad bits.
- `ShortcutConfig` is the persisted config (mode, hold, debounce, poll).
- `ShortcutDetector` is a deterministic state machine. It does **not**
  poll the pad itself; the caller feeds it `(buttons, now)` from any pad
  source. This makes detection fully unit-testable and lets the same code
  run from a host overlay app or a future payload-side polling loop.

### `UI`
- `CheatMenuModel` is a presentation-agnostic view-model.
- `ConsoleMenuRenderer` writes the model to any `TextWriter`.
- `menu/webui/index.html` is a starting-point browser UI consuming a
  companion HTTP server (your code) that fronts `CheatManager`.

### `Logging`
- `ILogger` with `NullLogger` and `ConsoleLogger` implementations.

## Threading

All public APIs are async and accept `CancellationToken`. The engine and
manager keep a `ConcurrentDictionary` of enabled cheats but otherwise have
no shared mutable state across instances.

## Extending

- **New cheat code type**: add a value to `CheatCodeType`, extend
  `CheatEngine.ResolveAsync`, document in `docs/CHEAT_FORMATS.md`,
  test in `PatchApplyTests`.
- **New format**: implement `ICheatFormatParser`; register with
  `CheatDatabaseService`.
- **Payload-side AOB**: keep the `INexusClient.ReadMemoryAsync` path as
  the fallback, add a new `IFastAobScan` capability interface, and let
  `AobScanner` prefer it when the implementation supports it.
