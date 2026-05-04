# ELF / PS5SDK / etaHEN Integration

> **Status:** Informational / Planning  
> **Last updated:** v0.2

---

## A. Current Supported Flow

```
┌─────────────────────────────────────────────────────────────┐
│  PS5 Console                                                 │
│                                                              │
│  ┌──────────────────────┐    HTTP (port 9090)                │
│  │ NexusFramework       │◄──────────────────────────┐        │
│  │ native payload (ELF) │                           │        │
│  │                      │  /status                  │        │
│  │  - /status           │  /read_memory             │        │
│  │  - /read_memory      │  /write_memory            │        │
│  │  - /write_memory     │  /get_process             │        │
│  │  - /get_process      │  /get_vm_maps             │        │
│  │  - /get_vm_maps      │  /aob_scan (stub)         │        │
│  │  - /aob_scan (stub)  │  /rpc_call                │        │
│  │  - /rpc_call         │                           │        │
│  └──────────────────────┘                           │        │
│                                                      │        │
│  ┌──────────────────────┐                           │        │
│  │ Game Process         │  Memory R/W via ptrace    │        │
│  │ (target app)         │◄──────────────────────────┘        │
│  └──────────────────────┘                                    │
└─────────────────────────────────────────────────────────────┘
                           ▲
                           │ HTTP (port 9080)
                           ▼
┌─────────────────────────────────────────────────────────────┐
│  PC / Mac / Linux Host                                       │
│                                                              │
│  ┌──────────────────────┐  ┌──────────────────────┐         │
│  │ NexusCheatFramework  │  │ NexusCheatFramework  │         │
│  │ .Web (ASP.NET Core)  │  │ .Cli (Console)       │         │
│  │                      │  │                      │         │
│  │  - /api/state        │  │  - status            │         │
│  │  - /api/process      │  │  - enable <id>       │         │
│  │  - /api/cheats       │  │  - disable <id>      │         │
│  │  - /api/enable       │  │  - scan <pattern>    │         │
│  │  - /api/disable      │  │  - list              │         │
│  │  - /api/scan         │  └──────────────────────┘         │
│  │  - /api/reload-cheats│                                    │
│  │  - /api/config       │                                    │
│  └──────────────────────┘                                    │
└─────────────────────────────────────────────────────────────┘
```

**Key fact:** The NexusFramework native payload must already be loaded on the PS5 for the cheat framework to function. The .NET projects on the host PC communicate with the payload over HTTP.

---

## B. Why `dotnet build` Does NOT Produce a PS5 ELF

| Aspect | .NET Projects (this repo) | PS5 Native Payload |
|--------|--------------------------|-------------------|
| Language | C# | C/C++ |
| Build tool | `dotnet build` | `make` + PS5SDK toolchain |
| Output | .dll / .exe (x86_64 host) | .elf (aarch64 PS5) |
| Runtime | .NET runtime on host | Runs directly on PS5 |
| Memory access | Via HTTP to payload | Direct ptrace/kernel |
| SDK needed | .NET SDK | PS5_PAYLOAD_SDK |

The existing .NET projects (`NexusCheatFramework.Core`, `.Web`, `.Cli`) are **host-side tools** that communicate with a PS5 payload over HTTP. They cannot be compiled into a PS5 ELF binary.

To build a native PS5 ELF, you need:

1. **PS5SDK** (or PS5_PAYLOAD_SDK) — the native C/C++ toolchain
2. A separate **native C/C++ payload project** (e.g., under `payload/ps5/`)
3. The native project must use PS5SDK conventions (entry point, headers, linker script)

---

## C. What PS5SDK Would Be Used For

[PS5Dev/PS5SDK](https://github.com/PS5Dev/PS5SDK) provides:

- **Cross-compiler toolchain** (aarch64-none-elf-*)
- **PS5-specific headers** (`<ps5/kernel.h>`, `<ps5/klog.h>`, `<ps5/mdbg.h>`, `<ps5/nid.h>`)
- **Linker scripts** and build templates
- **Payload entry point** conventions

A future native payload project under `payload/ps5/` would use PS5SDK to implement console-side endpoints:

| Endpoint | Purpose | Safety |
|----------|---------|--------|
| `/status` | Payload health check | Safe |
| `/read_memory` | Read game process memory | Offline only |
| `/write_memory` | Write cheat patches | Offline only |
| `/get_process` | Get running app PID/name | Safe |
| `/get_vm_maps` | Get memory region layout | Offline only |
| `/aob_scan` | Array-of-bytes pattern scan | Offline only |
| `/pad_state` | Read controller state | Safe |

**Important:** Only implement safe, offline memory operations. Do not implement:
- Anti-cheat bypass
- DRM/copyright protection bypass
- Piracy automation
- Online matchmaking abuse
- Credential theft

---

## D. etaHEN-Style Integration (Current)

The etaHEN-style flow today is:

```
etaHEN Toolbox entry
  └─► opens WebUI URL in PS4/PS5 browser
       └─► http://<host-pc-ip>:9080
            └─► WebUI controls NexusFramework payload
                 └─► payload reads/writes game memory
```

This is **not** a native cheat engine. It is a browser-based remote control flow:

1. Load NexusFramework payload on PS5 (via etaHEN, GoldHEN, or other loader)
2. Run `NexusCheatFramework.Web` on a PC on the same network
3. Open the WebUI URL from the PS5 browser (via etaHEN Toolbox entry or manually)
4. Use the WebUI to enable/disable cheats

The Toolbox XML entry is at:
```
menu/etaHEN_xml/toolbox_entry.xml
```

Installation:
```bash
# Copy to PS5
scp menu/etaHEN_xml/toolbox_entry.xml <console-ip>:/data/etaHEN/toolbox/nexus_cheats.xml
```

---

## E. Future Native ELF Path (Experimental)

A future native PS5 ELF cheat payload would follow this flow:

```
PS5SDK + payload/ps5/ native C/C++ code
  └─► make (with PS5_PAYLOAD_SDK set)
       └─► output/nexus-cheat-ps5.elf
            └─► load via compatible PS5 payload loader
                 └─► expose safe endpoints consumed by WebUI/CLI
```

The scaffold exists at:
```
payload/ps5/
```

**Status:** Experimental / Not buildable without PS5SDK installed.

### What would need to happen

1. Install PS5SDK: `git clone https://github.com/PS5Dev/PS5SDK`
2. Set `PS5_PAYLOAD_SDK` environment variable
3. Implement native endpoints in `payload/ps5/src/`
4. Build: `cd payload/ps5 && make`
5. Load the resulting `.elf` on PS5

### What the native payload would NOT do

- It would NOT replace the .NET WebUI/CLI — those remain the user interface
- It would NOT implement online cheating features
- It would NOT bypass anti-cheat systems
- It would NOT persist across reboots
- It would NOT include malicious code

---

## F. Comparison: NexusFramework vs. Standalone ELF

| Feature | Current (NexusFramework payload + .NET host) | Future (Standalone ELF) |
|---------|----------------------------------------------|------------------------|
| Build complexity | Low (dotnet build) | High (needs PS5SDK) |
| Memory access speed | Medium (HTTP round-trip) | Fast (direct) |
| AOB scan speed | Slow (client-side over HTTP) | Fast (native) |
| Controller polling | Via HTTP to payload | Direct |
| UI | Rich WebUI/CLI | Would still need WebUI/CLI |
| Portability | Cross-platform host | PS5 only |
| Development iteration | Fast (C# hot reload) | Slow (C/C++ cross-compile) |

---

## G. PS5SDK Payload Entry Point Convention

Based on PS5SDK examples, a native PS5 payload typically uses:

```c
#include <ps5/kernel.h>
#include <ps5/klog.h>

int payload_main(struct payload_args *args)
{
    // Initialize networking
    // Start HTTP server
    // Handle requests
    return 0;
}
```

The exact `payload_args` structure and entry point name should be verified from the PS5SDK documentation and examples before implementation.

---

## H. References

- **PS5SDK:** https://github.com/PS5Dev/PS5SDK
- **NexusFramework (upstream):** https://github.com/ItsJokerZz/NexusFramework
- **etaHEN:** https://github.com/etaHEN/etaHEN
- **NexusCheatFramework:** https://github.com/thatboialex/NexusFramework
- **Current payload patches:** `payload-patches/`
- **Native payload scaffold (experimental):** `payload/ps5/`
