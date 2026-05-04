# Nexus Payload HTTP API (subset used here)

NexusFramework exposes an HTTP server on port **9090** by default. The
endpoints below are everything the cheat framework currently needs. They
are reverse-engineered from the upstream C# client at
`NexusFramework/source/libraries/C#/commands/{connection,process}.cs` and
verified against the payload sources.

> If the payload spec changes upstream, both `HttpNexusClient` and
> `NexusClientAdapter` need to be updated.

## Connection

| Method | Path | Returns |
|---|---|---|
| GET | `/status` | `{ ... }` (any non-null JSON = alive) |
| GET | `/version` | `{ "VERSION": <float>, "NUMBER": <int>, "DATE": <string> }` |
| GET | `/connect` | acknowledgement |
| GET | `/disconnect` | acknowledgement |
| GET | `/unload` | unload payload |

## System & process info

| Method | Path | Returns |
|---|---|---|
| GET | `/get_sys_info` | `{ NAME, MODEL, USER, TYPE, FW, PSID, IDPS, CPU_FREQ, UPTIME, TEMPS:{CPU,SOC}, DISK:{TOTAL,FREE,USED,%} }` |
| GET | `/get_proc_list` | `{ "LIST": [ {AID,PID,EXEC,TID}, ... ] }` |
| GET | `/get_proc_info` | `{ AID,PID,TID,EXEC,NAME,REGION,SDK,TYPE,VER }` |
| GET | `/get_vm_maps` | `{ "maps": [ {NAME,START,END,OFFSET,PROT}, ... ] }` |

`PROT` is a uint flag with bits Read=1, Write=2, Execute=4, Copy=8.

## Memory

| Method | Path | Body / Query | Returns |
|---|---|---|---|
| GET | `/read_memory` | `?address=0xADDR&length=N` | `application/octet-stream` (raw bytes) |
| POST | `/write_memory` | JSON `{ "address":"0xADDR", "data":"<lowercase hex>" }` | acknowledgement |
| GET | `/memory_protection` | `?address=0xADDR&length=N&prot=<uint>` | acknowledgement |
| GET | `/allocate_memory` | `?length=N` | `"0xADDR"` |
| GET | `/free_memory` | `?address=0xADDR&length=N` | acknowledgement |

## Modules / shellcode (not used by v0.2)
| `/load_elf`, `/unload_elf`, `/load_module`, `/unload_module`,
| `/get_module_handle`, `/resolve_symbol`, `/install_shellcode`,
| `/detour_method`, `/rpc_call`, `/suspend_process`, `/resume_process`.

Shellcode/detour integration is documented future work (disabled by default
behind `allowAdvancedCodeExecution`).

## C# contract endpoints (optional, not payload-side)

### `AobScanAsync` — optional payload-side AOB scan

```csharp
Task<AobScanResult?> AobScanAsync(AobScanRequest request, CancellationToken ct);
```

**Status:** contract-only. `HttpNexusClient` implements this method but
returns `null` because the native payload does not expose a `/aob_scan`
endpoint. The C# `AobScanner` (client-side over `read_memory`) is the
functional fallback.

Request model:
```json
{
  "pattern": "48 8B ?? ?? 89",
  "start": "0x100000000",
  "end": "0x110000000",
  "maxResults": 8,
  "executableOnly": true
}
```

Response model:
```json
{
  "matches": ["0x100340a0", "0x10034b18"],
  "scannedRegions": 12,
  "skippedRegions": 3,
  "elapsedMs": 42
}
```

### `GetPadStateAsync` — optional payload-side pad polling

```csharp
Task<PadState?> GetPadStateAsync(int controllerIndex = 0, CancellationToken ct = default);
```

**Status:** contract-only. `HttpNexusClient` implements this method but
returns `null` because the native payload does not expose a `/pad_state`
endpoint. The `PadStatePollingService` is a host-side polling loop that
drives `ShortcutDetector`; without a payload endpoint it cannot read pad
state.

Response model:
```json
{
  "connected": true,
  "buttons": 257,
  "lx": 128,
  "ly": 128,
  "rx": 128,
  "ry": 128,
  "l2": 0,
  "r2": 0,
  "timestamp": 123456789
}
```

## Endpoints we'd like to add

| Endpoint | Purpose | Priority |
|---|---|---|
| `POST /aob_scan` | Payload-side AOB scanning (amortizes round-trip cost) | Medium |
| `GET /pad_state` | Read `scePadReadState` from payload | Medium |
| `POST /notify` | Pop a system toast / open URI from payload | Low |

Until these exist:
- `AobScanner` does AOB work over `read_memory` (slower but functional).
- `ShortcutDetector` runs against any pad source the host has access to
  (requires external pad input).
- Menu notifications are handled via the WebUI.

## Error format

Errors are returned as `{ ... }` JSON bodies. Both `HttpNexusClient` and
the upstream client treat any response starting with `{` after a
non-JSON-expected request as an error. Failures surface as exceptions
unless the caller used a strict-off code path.
