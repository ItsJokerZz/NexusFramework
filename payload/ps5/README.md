# PS5 Native Payload Scaffold (Experimental)

> **Status:** ⚠️ Experimental / Not buildable without PS5SDK  
> **Last updated:** v0.2

This directory contains an **experimental scaffold** for a future native PS5 ELF cheat payload.

---

## Purpose

The current NexusCheatFramework requires a NexusFramework payload already running on the PS5. This scaffold explores building a **standalone PS5 ELF** that could:

- Run independently on the PS5 (no separate NexusFramework payload needed)
- Provide native endpoints for memory reading/writing, AOB scanning, and controller polling
- Be consumed by the existing NexusCheatFramework WebUI and CLI

---

## Prerequisites

To build this scaffold, you need:

1. **PS5SDK** (https://github.com/PS5Dev/PS5SDK) — the native PS5 payload toolchain
2. **PS5_PAYLOAD_SDK** environment variable set to the PS5SDK path
3. A PS5 with a compatible payload loader (etaHEN, etc.)

---

## Build

```bash
# Set PS5SDK path
export PS5_PAYLOAD_SDK=/path/to/PS5SDK

# Build
cd payload/ps5
make

# Output: output/nexus-cheat-ps5.elf
```

---

## Structure

```
payload/ps5/
├── README.md              # This file
├── CMakeLists.txt         # CMake build (alternative to Makefile)
├── Makefile               # Make build (PS5SDK convention)
├── src/
│   └── main.c             # Payload entry point (placeholder)
└── include/
    └── payload.h          # Shared definitions
```

---

## Endpoints (Planned)

| Endpoint | Method | Description | Status |
|----------|--------|-------------|--------|
| `/status` | GET | Payload health check | TODO |
| `/read_memory` | POST | Read game process memory | TODO |
| `/write_memory` | POST | Write cheat patches | TODO |
| `/get_process` | GET | Get running app PID/name | TODO |
| `/get_vm_maps` | GET | Get memory region layout | TODO |
| `/aob_scan` | POST | Array-of-bytes pattern scan | TODO |
| `/pad_state` | GET | Read controller state | TODO |

---

## Safety

This native payload would:

- **Only** implement safe, offline memory operations
- **Not** implement anti-cheat bypass
- **Not** implement DRM/copyright protection bypass
- **Not** implement piracy automation
- **Not** implement online matchmaking abuse
- **Not** persist across reboots
- **Not** include malicious code

---

## References

- **PS5SDK:** https://github.com/PS5Dev/PS5SDK
- **NexusFramework (upstream):** https://github.com/ItsJokerZz/NexusFramework
- **NexusCheatFramework:** https://github.com/thatboialex/NexusFramework
- **Payload patches (reference):** `payload-patches/`
