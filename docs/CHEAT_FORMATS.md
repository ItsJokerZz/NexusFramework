# Cheat Formats

## Native JSON (supported, primary)

A single JSON file per Title ID is the supported format. Multiple files per
TID are allowed; they are concatenated at load time.

```json
{
  "titleId": "CUSA00000",
  "gameName": "Example Game",
  "version": "1.00",
  "region": "US",
  "cheats": [
    {
      "id": "infinite_health",
      "name": "Infinite Health",
      "description": "Optional human-readable note.",
      "enabledByDefault": false,
      "codes": [
        {
          "type": "aob_write_bytes",
          "aobPattern": "F3 0F 11 83 ?? ?? ?? ??",
          "offset": 0,
          "expectedBytes": "F3 0F 11 83 00 00 00 00",
          "bytes": "90 90 90 90 90 90 90 90"
        }
      ]
    }
  ]
}
```

### Top-level fields
| Field | Required | Notes |
|---|---|---|
| `titleId`  | **yes** | Sony Title ID, e.g. `CUSA00000`, `PPSA00000`. Used for filtering. |
| `gameName` | no | Display only. |
| `version`  | no | Display only; the engine does not gate on this yet. |
| `region`   | no | Display only. |
| `cheats[]` | no | Empty is allowed (no-op). |

### `cheats[]` entries
| Field | Required | Notes |
|---|---|---|
| `id` | **yes** | Stable identifier. CLI uses this. |
| `name` | **yes** | Display name. |
| `description` | no | |
| `enabledByDefault` | no | Parsed but not auto-applied on connect. Planned feature. |
| `codes[]` | **yes** | Must have at least one entry. |

### `codes[]` entries — supported types

| Type | Description | Required fields | Optional fields |
|---|---|---|---|
| `write_bytes` | Write raw bytes to an absolute address | `address`, `bytes` | `offset`, `expectedBytes` |
| `write_value` | Write a typed value to an absolute address | `address`, `valueType`, `value` | `offset`, `expectedBytes` |
| `aob_write_bytes` | Find AOB pattern, write bytes at match+offset | `aobPattern`, `bytes` | `offset`, `expectedBytes` |
| `aob_write_value` | Find AOB pattern, write value at match+offset | `aobPattern`, `valueType`, `value` | `offset`, `expectedBytes` |
| `module_write_bytes` | Find executable module base, write bytes at base+offset | `moduleName`, `bytes` | `offset`, `expectedBytes` |
| `module_write_value` | Find executable module base, write value at base+offset | `moduleName`, `valueType`, `value` | `offset`, `expectedBytes` |
| `freeze_value` | Write a typed value in a loop at an absolute address | `address`, `valueType`, `value` | `offset`, `expectedBytes`, `freezeIntervalMs` |
| `pointer_write_bytes` | Resolve pointer chain from base address, write bytes | `address`, `pointerOffsets[]`, `bytes` | `offset`, `expectedBytes` |
| `pointer_write_value` | Resolve pointer chain from base address, write value | `address`, `pointerOffsets[]`, `valueType`, `value` | `offset`, `expectedBytes` |
| `aob_pointer_write_bytes` | Find AOB pattern, resolve pointer chain from match, write bytes | `aobPattern`, `aobOffset`, `pointerOffsets[]`, `bytes` | `offset`, `expectedBytes` |
| `aob_pointer_write_value` | Find AOB pattern, resolve pointer chain from match, write value | `aobPattern`, `aobOffset`, `pointerOffsets[]`, `valueType`, `value` | `offset`, `expectedBytes` |

### Example: freeze_value with pointer chain (workaround)

`freeze_value` currently only supports absolute addresses. To freeze a value
at a dynamically resolved address, combine a `pointer_write_value` code with
a scheduler loop on the host side. Alternatively, define the cheat with
two codes: one `pointer_write_value` for initial application, and a
`freeze_value` code at the resolved address if it is stable across runs.

```json
{
  "id": "freeze_ammo",
  "name": "Freeze Ammo",
  "codes": [
    {
      "type": "freeze_value",
      "address": "0x12345678",
      "valueType": "int",
      "value": "99",
      "freezeIntervalMs": 250
    }
  ]
}
```

```json
{
  "id": "pointer_freeze_ammo",
  "name": "Pointer-Freeze Ammo",
  "codes": [
    {
      "type": "pointer_write_value",
      "address": "0x100000000",
      "pointerOffsets": ["0x20", "0x18", "0x40"],
      "valueType": "int",
      "value": "999"
    }
  ]
}
```

### Freeze loop behavior

- Each `freeze_value` code starts a background loop that writes the value
  at `intervalMilliseconds` cadence (default 250ms).
- The loop stops when the cheat is disabled, the process disconnects, or
  the `CheatEngine` is disposed.
- Only one freeze loop runs per cheat ID; duplicate calls are ignored.
- Freeze writes that fail (e.g. process crashed) log a warning and break
  the loop.

### Pointer chain resolution

- `pointerOffsets` is an ordered list of signed or hex offsets.
- Every offset except the last is a *pointer read*: the engine reads 8 bytes
  at `(current + offset[i])` and sets `current` to the resulting ulong.
- The last offset is an *addition*: `current += offset[last]` yields the
  final address.
- If a pointer resolves to `0x0`, the chain throws an error.

### `freezeIntervalMs`

Used only for `freeze_value` type. Default 250ms. Valid range: 1–60000.

### `aobOffset`

Used only with `aob_pointer_write_*` types. Signed offset from the AOB
match address where the pointer chain starts.

### Type names

Type names are case-insensitive; both snake_case (`aob_write_bytes`) and
PascalCase (`AobWriteBytes`) are accepted.

`address` and `offset` accept decimal or `0x`-prefixed hex. `offset` may
be negative.

`bytes` and `expectedBytes` accept space, dash, or comma separators
(`"90 90"`, `"90-90"`, `"9090"` all work).

### Supported `valueType`s
`byte`/`u8`, `sbyte`/`i8`, `short`/`i16`, `ushort`/`u16`,
`int`/`i32`, `uint`/`u32`, `long`/`i64`, `ulong`/`u64`,
`float`/`f32`, `double`/`f64`, `string`/`utf8`.

Numeric values use invariant culture. Floats are little-endian.

### Safety: `expectedBytes`
If a code provides `expectedBytes`, the engine reads the live bytes first
and refuses to apply the cheat if they don't match. Override with
`CheatRuntimeOptions.ForceApply = true` (or `--force` on the CLI). When
the cheat is later disabled, the *original live* bytes are written back —
not `expectedBytes`. This means even a force-applied cheat can be cleanly
restored.

## etaHEN cheat format

**Status:** not supported. etaHEN's "Cheats (WIP)" entry is a menu page
without a public, documented file format in the bundled sources. The
`EtaHenCheatParser` is a stub that throws `NotSupportedException` with a
pointer to this document.

If you have a sample file from a future etaHEN release, please open an
issue with the spec.

## `.shn` (Illusion-cheats)

**Status:** not supported. The format isn't documented in either upstream.
`ShnCheatParser` is a stub.

## `.mc4`

**Status:** not supported. Same situation as `.shn`.

## Migrating from other tools

If you have cheats authored for another framework, the recommended path is
to write a one-off converter that produces the JSON format above. The
schema is intentionally narrow and stable.
