# Nexus Payload HTTP API (subset used here)

NexusFramework exposes an HTTP server on port **9090** by default. The
endpoints below are everything the cheat framework currently needs. They
are reverse-engineered from the upstream C# client at
`NexusFramework/source/libraries/C#/commands/{connection,process}.cs` and
verified against the payload sources in
`NexusFramework/source/console/source/api_commands.cpp`.

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

## Modules / shellcode (not used by v0.1)
| `/load_elf`, `/unload_elf`, `/load_module`, `/unload_module`,
| `/get_module_handle`, `/resolve_symbol`, `/install_shellcode`,
| `/detour_method`, `/rpc_call`, `/suspend_process`, `/resume_process`.

## Endpoints we'd like to add

### `aob_scan` (proposed)
Payload-side AOB matching to amortize the network round-trip cost.

```
POST /aob_scan
{
  "pattern": "48 8B ?? ?? 89",
  "start": "0x100000000",
  "end":   "0x110000000",
  "max_results": 8,
  "executable_only": true
}

200 OK
{ "matches": ["0x100340a0","0x10034b18"] }
```

Until that exists, `AobScanner` does the same work over `read_memory`.

### `pad_state` (proposed)
Read the active controller state for shortcut handling.

```
GET /pad_state
200 OK
{ "buttons": 0x101, "lx": 128, "ly": 128, "rx": 128, "ry": 128, "l2": 0, "r2": 0 }
```

Until that exists, `ShortcutDetector` runs against any pad source the host
has access to.

### `notify` (proposed)
Pop a system toast / open a URI from the payload, mirroring etaHEN's
`GoToURI("etaHEN?Cheats")` so a shortcut can open the menu in-game
without a shellui hook.

## Error format

Errors are returned as `{ ... }` JSON bodies. Both `HttpNexusClient` and
the upstream client treat any response starting with `{` after a
non-JSON-expected request as an error. Failure surface up as exceptions
unless the caller used a strict-off code path.
