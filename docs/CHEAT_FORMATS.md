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
| `enabledByDefault` | no | Reserved; the engine does not auto-enable yet. |
| `codes[]` | **yes** | Must have at least one entry. |

### `codes[]` entries
| Type | Required fields | Optional fields |
|---|---|---|
| `write_bytes`        | `address`, `bytes` | `offset`, `expectedBytes` |
| `write_value`        | `address`, `valueType`, `value` | `offset`, `expectedBytes` |
| `aob_write_bytes`    | `aobPattern`, `bytes` | `offset`, `expectedBytes` |
| `aob_write_value`    | `aobPattern`, `valueType`, `value` | `offset`, `expectedBytes` |
| `module_write_bytes` | `moduleName`, `bytes` | `offset`, `expectedBytes` |

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
