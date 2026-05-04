# NexusCheatFramework

A NexusFramework-based cheat framework for **PS4 / PS5 homebrew, research,
and accessibility** scenarios. It combines:

- **NexusFramework** (MIT, ItsJokerZz) — remote management payload + C# client
  for memory read/write, process listing, VM maps, ELF/module loading, RPC,
  and shellcode detours.
- **etaHEN** patterns and ideas (GPLv3, etaHEN team) — the toolbox menu/XML
  layout, the controller-shortcut UX (`Hold R3+L3`, `Hold L2+△`, long/short
  Share/Options taps), and the cheat-menu entry point.

> **Repo location note.** The hosted repo this branch lives in is
> `thatboialex/nexusframework`; my MCP tools are scoped to that single repo,
> so this project ships as the `NexusCheatFramework/` directory at repo root
> on the `claude/merge-reverse-engineering-frameworks-mpqPb` branch instead
> of as a freshly created top-level repository. Move it to its own repo when
> ready — it has no hard-coded path assumptions.

---

## What this is

- A cheat engine layer on top of NexusFramework's HTTP payload API.
- A **finished, tested AOB/pattern scanner** (the upstream's `ArrayOfBytesScan`
  was marked `// Not yet implemented on the API/payload (backend)` — see
  `NexusFramework/source/libraries/C#/commands/process.cs:363`).
- A native JSON cheat format with a sample database.
- An `INexusClient` abstraction so memory/process operations are mockable;
  HTTP-backed implementation is included.
- A controller-shortcut state machine modeled on etaHEN's UX.
- An etaHEN-toolbox XML drop-in plus a small WebUI for an actual menu.
- A CLI (`ncf`) for connect / scan / cheats list / enable / disable.
- xUnit tests for pattern parsing, scanner correctness (incl. chunk
  boundaries), JSON parsing, patch apply/restore, and shortcut detection.

## What this is **not**

- Not an online-cheating tool. Don't use it in matchmaking or competitive
  play. See [docs/SECURITY_AND_ETHICS.md](docs/SECURITY_AND_ETHICS.md).
- Not a full in-game overlay. Console-side rendering would require either a
  Mono/shellui hook (etaHEN's territory, GPLv3) or a graphics-API detour
  (heavy and game-specific). The cheat menu is delivered via a WebUI/CLI
  companion plus an etaHEN toolbox XML link.
- Not an etaHEN replacement; it depends on having `etaHEN` (or any other
  jailbreak) available to load the Nexus payload in the first place.

## Supported platforms / requirements

| | |
|---|---|
| Console | PS4 (jailbroken, GoldHEN ≥ v2.4b18.8 with ELF support, or `elfldr`) |
|         | PS5 (jailbroken, etaHEN toolbox payload loader or `elfldr` socket) |
| Build PC | .NET SDK 8.0+ (Core lib targets `netstandard2.1`) |
| Network | Console and PC on the same LAN; Nexus HTTP on port 9090 by default |

## Quick start

```sh
# 1. Build
./scripts/build.sh         # or scripts/build.ps1 on Windows

# 2. Send the Nexus payload to your console (use NexusFramework's loader,
#    etaHEN toolbox, GoldHEN, or elfldr).
#    Once it's running, the HTTP API is reachable at :9090.

# 3. Sanity-check the connection
dotnet run --project src/NexusCheatFramework.Cli -- info --ip 192.168.1.50

# 4. Scan
dotnet run --project src/NexusCheatFramework.Cli -- \
    scan --ip 192.168.1.50 --pattern "48 8B ?? ?? 89" --all --exec

# 5. List cheats for the active TID using the included sample DB
dotnet run --project src/NexusCheatFramework.Cli -- \
    cheats list --ip 192.168.1.50 --db ./examples/CheatFormatSamples

# 6. Enable / disable
dotnet run --project src/NexusCheatFramework.Cli -- \
    cheats enable  --ip 192.168.1.50 --db ./examples/CheatFormatSamples --cheat infinite_health
dotnet run --project src/NexusCheatFramework.Cli -- \
    cheats disable --ip 192.168.1.50 --db ./examples/CheatFormatSamples --cheat infinite_health
```

## Cheat files

Native JSON format — see [docs/CHEAT_FORMATS.md](docs/CHEAT_FORMATS.md) and
[`examples/CheatFormatSamples/CUSA00000-example.json`](examples/CheatFormatSamples/CUSA00000-example.json).

Code types currently supported:

| Type | Resolves to | Notes |
|---|---|---|
| `write_bytes` | `address + offset` | static absolute |
| `write_value` | `address + offset` | typed (`i32`, `f32`, `u64`, `utf8`, …) |
| `aob_write_bytes` | `aob_match + offset` | uses the AOB scanner |
| `aob_write_value` | `aob_match + offset` | typed write at AOB hit |
| `module_write_bytes` | `module.start + offset` | resolves via `get_vm_maps` |

Each code may set `expectedBytes`; the engine refuses to apply if live
bytes don't match (override with `--force` / `ForceApply`).

## AOB scanning

- Streams memory in chunks (default 256 KiB) with `pattern.Length-1` overlap so
  matches that straddle a chunk boundary are not missed.
- Filters regions by `readable` / `executable` / `name contains` / address
  range.
- `FindFirstAsync` and `FindAllAsync`, with `CancellationToken` support.
- Pattern syntax accepts `48 8B ?? ?? 89`, `48 8B ? ? 89`, `48 8B ** ** 89`
  and run-together forms like `488B????89`.

See [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) for the layer diagram.

## Menu integration

- `menu/etaHEN_xml/nexus_cheats.xml` — drop-in shellui page for the etaHEN
  toolbox. Adds an "★ Nexus Cheats" entry that explains how to reach the
  WebUI and CLI companion.
- `menu/etaHEN_xml/toolbox_link.snippet.xml` — the single-line edit you
  paste into etaHEN's `etaHEN_toolbox.xml` next to `id_cheats`.
- `menu/webui/index.html` — small browser UI consuming a future companion
  HTTP endpoint (`/api/state`, `/api/enable`, `/api/disable`). The endpoints
  are documented in [docs/MENU_INTEGRATION.md](docs/MENU_INTEGRATION.md);
  hosting the API is left to consumers (the CLI shows the underlying
  `CheatManager` calls).

## Controller shortcuts

[docs/CONTROLLER_SHORTCUTS.md](docs/CONTROLLER_SHORTCUTS.md). The
`ShortcutDetector` is a pure state machine that takes pad samples and a
clock and raises an event when the configured combo fires. Hooking the
detector to live `scePadReadState` requires payload-side support (see
[docs/LIMITATIONS.md](docs/LIMITATIONS.md)).

## License

GPL-3.0-or-later. See [LICENSE](LICENSE) and [ATTRIBUTION.md](ATTRIBUTION.md)
for why GPL was chosen and what was reused from each upstream.

## Safety / legal

Read [docs/SECURITY_AND_ETHICS.md](docs/SECURITY_AND_ETHICS.md). One-line
version: own the console, own the game, stay offline. You assume all risk.
