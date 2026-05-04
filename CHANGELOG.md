# Changelog

## v0.1.0-alpha (initial)

- Core library targeting `netstandard2.1`.
- `INexusClient` abstraction + `HttpNexusClient` over the NexusFramework
  payload HTTP API.
- Optional `NexusClientAdapter` wrapping the upstream NexusFramework C#
  library (off-by-default project).
- Finished AOB scanner with chunked reads, overlap, region filtering,
  cancellation, and progress callback.
- Native JSON cheat format with five code types
  (`write_bytes`, `write_value`, `aob_write_bytes`, `aob_write_value`,
  `module_write_bytes`).
- `EtaHenCheatParser`, `ShnCheatParser`, `Mc4CheatParser` stubs that throw
  with helpful messages — see `docs/CHEAT_FORMATS.md`.
- `CheatEngine` with original-bytes storage, restore-on-disable,
  expected-bytes safety check, dry-run mode, and structured `CheatResult`.
- `ShortcutDetector` state machine for the etaHEN-style controller combos.
- Drop-in etaHEN toolbox XML + WebUI scaffold for the cheat menu.
- xUnit tests for pattern parsing, AOB scanning (incl. chunk boundary
  case), JSON parsing, patch apply/restore/expected-bytes safety, and
  shortcut detection.
- CLI: `ncf {info,scan,cheats list/enable/disable}`.
