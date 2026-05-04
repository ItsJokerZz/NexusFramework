# NexusCheatFramework v0.2

A **build-verified, runnable** cheat engine framework for PlayStation 4/5 homebrew,
built on top of **NexusFramework** and informed by **etaHEN**.

> **Legal / Ethical Scope**  
> This project is for **user-owned consoles, user-owned games, offline homebrew,
> research, debugging, and accessibility** use only.  
> **Do not** use for: online cheating, matchmaking abuse, anti-cheat bypasses,
> piracy automation, credential theft, or service abuse.  
> See [SECURITY_AND_ETHICS.md](docs/SECURITY_AND_ETHICS.md).

---

## What this is

- A real cheat engine framework that connects to a PlayStation 4/5 console running
  a NexusFramework payload.
- Cheat database loading from JSON files.
- Pattern/AOB scanning (client-side over HTTP; optional payload-side acceleration).
- Cheat application with freeze loops, pointer chains, and AOB resolution.
- A **WebUI** backend (ASP.NET Core Minimal API) serving a browser-based cheat toggler.
- A **CLI** front-end for automation/scripts.
- Controller shortcut detection (state machine + optional payload polling).
- etaHEN Toolbox XML integration.

## What this is NOT

- Not a turnkey “generate cheats” tool.
- Not a PS4/5 jailbreak.
- Not a piracy tool.
- Not a matchmaking or anti-cheat bypass.

## Supported Platforms

| Platform | Status |
|----------|--------|
| **PC (hosting WebUI)** | Windows, Linux, macOS |
| **Console (payload)** | PS4 / PS5 with NexusFramework payload loaded |

## Requirements

| Dependency | Version | Purpose |
|------------|---------|---------|
| [.NET SDK](https://dotnet.microsoft.com/download) | 8.0.x | Build and run all projects |
| Console | any | Must have NexusFramework payload running (port 9090 by default) |

## Quick Start

```bash
# 1. Clone
git clone https://github.com/thatboialex/NexusFramework.git
cd NexusFramework

# 2. Build
dotnet build -c Release

# 3. Run tests
dotnet test -c Release

# 4. Launch WebUI (replace <console-ip> with your PS4/PS5 IP)
dotnet run --project src/NexusCheatFramework.Web -- --ip 192.168.1.100 --port 9080

# 5. Open http://localhost:9080 in a browser
```

### CLI Quick Start

```bash
# Scan memory for an AOB pattern
dotnet run --project src/NexusCheatFramework.Cli -- scan --ip 192.168.1.100 "48 8B 05 ?? ?? ?? 89"

# List game info
dotnet run --project src/NexusCheatFramework.Cli -- info --ip 192.168.1.100
```

## Repository Structure

```
NexusCheatFramework/
  .github/workflows/ci.yml     — GitHub Actions CI
  src/
    NexusCheatFramework.Core/   — Shared core library (netstandard2.1)
      Core/                     — CheatEngine, CheatManager, CheatResult, etc.
      Formats/                  — CheatModel, JSON/etaHEN/.shn/.mc4 parsers
      Input/                    — PadButton, ShortcutDetector, ShortcutConfig
      Logging/                  — ILogger interface + NullLogger + ConsoleLogger
      Memory/                   — AobPattern, AobScanner, MemoryPatch
      Nexus/                    — INexusClient + HttpNexusClient
      Services/                 — CheatDatabaseService, PadStatePollingService
      UI/                       — CheatMenuModel, ConsoleMenuRenderer
    NexusCheatFramework.Cli/    — CLI frontend (net8.0)
    NexusCheatFramework.Web/    — WebUI backend (ASP.NET Core, net8.0)
  tests/
    NexusCheatFramework.Tests/  — xUnit test suite
  menu/
    webui/index.html            — Browser-based cheat menu
    etaHEN_xml/                 — etaHEN Toolbox integration XML
  docs/                         — Full documentation
  examples/                     — Sample cheat databases
  scripts/                      — Build/test helper scripts
```

## Cheat Format Example

```json
{
  "titleId": "CUSA00001",
  "gameName": "Example Game",
  "version": "1.00",
  "region": "US",
  "cheats": [
    {
      "id": "inf_health",
      "name": "Infinite Health",
      "description": "Health always stays at 999",
      "enabledByDefault": false,
      "codes": [
        {
          "type": "freeze_value",
          "address": "0x12345678",
          "valueType": "float",
          "value": "100.0",
          "freezeIntervalMs": 250
        }
      ]
    },
    {
      "id": "inf_ammo",
      "name": "Infinite Ammo",
      "codes": [
        {
          "type": "pointer_write_value",
          "address": "0x100000000",
          "pointerOffsets": ["0x20", "0x18", "0x40"],
          "valueType": "int",
          "value": "99"
        }
      ]
    }
  ]
}
```

## Implemented Cheat Types (v0.2)

| Type | Description |
|------|-------------|
| `write_bytes` | Write raw bytes at absolute address |
| `write_value` | Write typed value at absolute address |
| `aob_write_bytes` | Resolve AOB pattern, write bytes at match |
| `aob_write_value` | Resolve AOB pattern, write typed value |
| `module_write_bytes` | Module base + offset, write bytes |
| `module_write_value` | Module base + offset, write typed value |
| `freeze_value` | Continuously write a value at interval |
| `pointer_write_bytes` | Resolve pointer chain, write bytes |
| `pointer_write_value` | Resolve pointer chain, write typed value |
| `aob_pointer_write_bytes` | AOB match → pointer chain → write bytes |
| `aob_pointer_write_value` | AOB match → pointer chain → write typed value |

## WebUI Endpoints

| Method | Path | Description |
|--------|------|-------------|
| GET | `/api/state` | Full connection/process/cheat/logs state |
| GET | `/api/process` | Current game process info |
| GET | `/api/cheats` | List loaded cheats with enabled status |
| POST | `/api/enable` | Enable a cheat `{ "id": "cheat_id" }` |
| POST | `/api/disable` | Disable a cheat `{ "id": "cheat_id" }` |
| POST | `/api/scan` | AOB scan `{ "pattern": "...", "maxResults": 8 }` |
| POST | `/api/reload-cheats` | Reload cheat database from disk |
| GET | `/api/config` | Current server configuration |
| POST | `/api/config` | Config update (requires restart) |

## Menu Integration

- **WebUI**: `http://localhost:9080` — full browser-based cheat menu
- **etaHEN Toolbox**: See [menu/etaHEN_xml/](menu/etaHEN_xml/) for XML entries
  that can open NCF WebUI in the PS4 browser

## Controller Shortcuts

| Mode | Description | Status |
|------|-------------|--------|
| HoldR3L3 | Hold R3+L3 together | Implemented (state machine) |
| HoldL2Triangle | Hold L2+Triangle | Implemented (state machine) |
| LongHoldOptions | Hold Options for 2s | Implemented (state machine) |
| LongHoldShare | Hold Share for 2s | Implemented (state machine) |
| SingleTapShare | Single-tap Share | Implemented (state machine) |
| Live polling | `/pad_state` payload endpoint | Documented — requires payload-side build |

> **Note:** Share/Create button may be intercepted by system UI.  
> HoldR3L3 and HoldL2Triangle are the most reliably supported modes.

## API Status (Payload Integration)

| Endpoint | Client-Side | Payload-Side (optional) |
|----------|-------------|------------------------|
| `/read_memory` | ✅ Via `HttpNexusClient` | Native Nexus payload |
| `/write_memory` | ✅ Via `HttpNexusClient` | Native Nexus payload |
| `/aob_scan` | ✅ Client-side fallback via read_memory | 📄 Contract defined, payload implementation pending native build |
| `/pad_state` | 📄 Contract defined, returns null when unavailable | 📄 Contract defined, implementation pending native build |

## Can I Build This as a PS5 ELF?

**No, not as-is.** This repository builds .NET projects (WebUI/CLI/Core) that run on a host PC. They communicate with a NexusFramework payload already running on the PS5 over HTTP.

### What this repo builds

```
dotnet build -c Release
  └─► NexusCheatFramework.Core.dll   (shared library, netstandard2.1)
  └─► NexusCheatFramework.Web.dll    (ASP.NET Core WebUI, net10.0)
  └─► ncf.dll                        (CLI frontend, net10.0)
```

These are **host-side tools** — they run on your PC, not on the PS5.

### What a PS5 ELF requires

A native PS5 ELF cheat payload would need:

1. **PS5SDK** (https://github.com/PS5Dev/PS5SDK) — the native C/C++ toolchain
2. A separate **native C/C++ payload project** (e.g., under `payload/ps5/`)
3. PS5SDK does **not** compile .NET projects into an ELF

### Current etaHEN-style flow

```
etaHEN Toolbox entry
  └─► opens WebUI URL in PS4/PS5 browser
       └─► http://<host-pc-ip>:9080
            └─► WebUI controls NexusFramework payload
                 └─► payload reads/writes game memory
```

This is a **browser-based remote control flow**, not a native cheat engine.

### Future native ELF path

An experimental scaffold exists at `payload/ps5/` for a future native PS5 ELF:

```
PS5SDK + payload/ps5/ native C/C++ code
  └─► make (with PS5_PAYLOAD_SDK set)
       └─► output/nexus-cheat-ps5.elf
            └─► load via compatible PS5 payload loader
                 └─► expose safe endpoints consumed by WebUI/CLI
```

**Status:** Experimental / Not buildable without PS5SDK installed.

See [docs/ELF_PS5SDK_ETAHEN_INTEGRATION.md](docs/ELF_PS5SDK_ETAHEN_INTEGRATION.md) for full details.

## Current Limitations

See [docs/LIMITATIONS.md](docs/LIMITATIONS.md) for the full list.

Key limitations:
- No native PS4/PS5 payload build toolchain in this repo — payload endpoints
  depend on NexusFramework's payload SDK.
- Controller shortcut live polling requires payload-side `/pad_state` endpoint.
- Shellcode/detour actions are documented future work (disabled by default).
- etaHEN (.shn, .mc4) format parsers are stubs (no public spec found).
- On Windows, the WebUI path auto-detection uses upward directory traversal.
- **Cannot build a standalone PS5 ELF** from this repo as-is — requires PS5SDK
  and a separate native C/C++ payload project.

## Build Commands

```bash
# Full build
dotnet build -c Release

# Full test
dotnet test -c Release

# Format check
dotnet format --verify-no-changes

# Build scripts (cross-platform)
./scripts/build.sh        # Linux/macOS
./scripts/build.ps1       # Windows PowerShell
```

## Credits / Attribution

- **NexusFramework** (MIT) — primary foundation, payload protocol, memory primitives.
  See [ATTRIBUTION.md](ATTRIBUTION.md).
- **etaHEN** (GPLv3) — concepts referenced for shortcut detection behavior,
  toolbox XML format. No code copied. See [ATTRIBUTION.md](ATTRIBUTION.md).
- This project: MIT for original code. Any etaHEN-derived portions are GPLv3-compatible.

## License

- Original NexusCheatFramework code: **MIT**
- Adaptation of any etaHEN-derived work (if applicable): **GPLv3**
- See [LICENSE](LICENSE) and [ATTRIBUTION.md](ATTRIBUTION.md).
