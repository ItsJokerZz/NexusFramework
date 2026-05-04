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
+─────── PS4 / PS5 (Nexus payload) ──────────+
|  read_memory / write_memory / get_vm_maps   |
|  get_proc_info / memory_protection / …      |
|  (optional: aob_scan*, pad_state* — planned)|
+────────────────────────────────────────────-+
```

\* `aob_scan` and `pad_state` are contract-only on the C# client. The native
payload endpoints are not implemented.

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
- `JsonCheatParser` is the supported, documented parser (v0.2: freeze_value,
  pointer chains, AOB pointer chains, module writes).
- `EtaHenCheatParser`, `ShnCheatParser`, `Mc4CheatParser` are stubs that
  throw with helpful messages until those formats are documented.

### `Core` — orchestration
- `CheatEngine` owns enable/disable for a single connection. It resolves
  each `CheatCode` to `(address, patchBytes)`, reads originals, optionally
  validates against `expectedBytes`, and tracks applied patches per cheat
  ID. Disable restores originals (configurable). Freeze loops run in
  background tasks with cancellation support.
- `CheatManager` couples the engine to a database and an active process.
  It loads cheat files, filters by Title ID, and exposes the list to the
  UI layer.
- `CheatRuntimeOptions` controls safety / dry-run / ambiguous-match
  policy.

### `Services` — host-side polling
- `PadStatePollingService` — configurable polling loop that feeds
  controller state into `ShortcutDetector`. Driven by cancellation token.
  Returns no data until the payload exposes `scePadReadState`.

### `Input` — controller shortcuts
- `PadButton` is a flag enum mirroring Sony / OpenOrbis pad bits.
- `ShortcutConfig` is the persisted config (mode, hold, debounce, poll).
- `ShortcutDetector` is a deterministic state machine. It does **not**
  poll the pad itself; the caller feeds it `(buttons, now)` from any pad
  source.

### `UI`
- `CheatMenuModel` is a presentation-agnostic view-model.
- `ConsoleMenuRenderer` writes the model to any `TextWriter`.
- `menu/webui/index.html` — browser UI consuming the Web API.
- `src/NexusCheatFramework.Web/` — ASP.NET Core Minimal API host.

### `Logging`
- `ILogger` with `NullLogger`, `ConsoleLogger`, and `WebUILogger`
  implementations.

## Threading

All public APIs are async and accept `CancellationToken`. The engine and
manager keep a `ConcurrentDictionary` of enabled cheats but otherwise have
no shared mutable state across instances. Freeze loops run on background
tasks; each loop has its own `CancellationTokenSource`.

## Extending

- **New cheat code type**: add a value to `CheatCodeType`, extend
  `CheatEngine.ResolveAsync`, document in `docs/CHEAT_FORMATS.md`,
  test in `CheatEngineTests`.
- **New format**: implement `ICheatFormatParser`; register with
  `CheatDatabaseService`.
- **Payload-side AOB**: keep the `AobScanner.ReadMemoryAsync` path as
  the fallback; `INexusClient.AobScanAsync` defines the optional fast path.
