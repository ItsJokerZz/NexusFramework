# Changelog

## v0.2 — Build-verified, runnable release

### Build & CI
- ✅ Full build passes (`dotnet build -c Release`)
- ✅ 50 tests pass (`dotnet test -c Release`)
- ✅ `.github/workflows/ci.yml` — GitHub Actions CI on push/PR
- ✅ `.gitignore` updated for .NET, IDE, secrets, OS files

### Core — Cheat Engine v0.2
- **CheatModel** — new types: `FreezeValue`, `PointerWriteBytes`, `PointerWriteValue`,
  `AobPointerWriteBytes`, `AobPointerWriteValue`, `ModuleWriteBytes`, `ModuleWriteValue`
- **CheatEngine** — freeze loops (configurable interval, cancellable, per-cheat),
  pointer chain resolution (module + offsets), AOB pointer chains, rollback on failure
- **JsonCheatParser** — updated to parse all v0.2 fields (`freezeIntervalMs`,
  `pointerOffsets`, `aobOffset`, `enabledByDefault`)

### WebUI Backend (NEW)
- `src/NexusCheatFramework.Web/` — ASP.NET Core Minimal API
- Implements all 9 endpoints: `/api/state`, `/api/process`, `/api/cheats`,
  `/api/enable`, `/api/disable`, `/api/scan`, `/api/reload-cheats`,
  `/api/config` (GET + POST)
- Arg parsing: `--ip`, `--port`, `--console-port`, `--db`, `--verbose`
- Serves `menu/webui/index.html` with connection status, cheat list, AOB scanner

### Menu / Integration
- `menu/webui/index.html` — Updated with connection badge, log viewer, AOB scan form
- `menu/etaHEN_xml/toolbox_entry.xml` — Functional etaHEN Toolbox XML entry
- `PadStatePollingService` — Optional polling service driving `ShortcutDetector`
- Documented controller shortcut modes and limitations

### Platform Support
- `INexusClient` — extended with `AobScanAsync` and `GetPadStateAsync`
- `HttpNexusClient` — implements both optional endpoints (return null when unavailable)
- Payload-side endpoint contracts documented

### Tests (50 total)
- AOB pattern parser: exact bytes, wildcards, compact hex, invalid, empty
- AOB scanner: first match, all matches, no match, cancellation
- Cheat engine: enable/disable, expected bytes, dry-run, force-apply, rollback
- Freeze: loop verification, disable stops loop
- JSON parser v0.2: freeze, pointer, AOB-pointer, module, error cases
- Shortcut detector: all modes, debounce

### Documentation
- `README.md` — full rewrite with quick start, structure, feature table
- `CHANGELOG.md` — this file
- All docs updated to reflect v0.2 reality

### Limitations (honest)
- No native PS4/PS5 payload build toolchain
- `/aob_scan` and `/pad_state` payload endpoints are contract-defined only
- Shellcode/detour actions: documented future work
- etaHEN .shn/.mc4 parsers remain stubs (no public spec found)

## v0.1 — Initial scaffold

- Project structure with solution, core library, CLI adapter
- Basic cheat definition model and JSON parser
- AOB pattern parsing and scanning
- Initial test suite
- Documentation scaffold
