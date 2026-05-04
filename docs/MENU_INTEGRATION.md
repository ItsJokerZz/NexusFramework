# Menu Integration

## TL;DR

There are two complementary menu paths:

1. **etaHEN toolbox link** — adds a "★ Nexus Cheats" entry to etaHEN's
   toolbox so it's discoverable from the console. The page explains how
   to reach the live WebUI.
2. **WebUI backend** (shipped) — ASP.NET Core Minimal API serving
   `menu/webui/index.html` on `http://0.0.0.0:9080/`.

True in-game overlay rendering is **not implemented**. See
`docs/LIMITATIONS.md` for why.

## Path 1 — etaHEN toolbox

1. Copy `menu/etaHEN_xml/toolbox_entry.xml` next to etaHEN's
   `etaHEN_toolbox.xml` (typically packaged with shellui assets).
2. Open `etaHEN_toolbox.xml` and add the link:

   ```xml
   <link id="id_nexus_cheats" title="★ Nexus Cheats" file="toolbox_entry.xml"/>
   ```

3. If etaHEN's assets are encrypted on your build, re-encrypt with
   `ETAHEN/shellui/assets/encryptxml.py` (etaHEN's own tool; not shipped).
4. Reboot the toolbox; the entry appears alongside "Cheats (WIP)".

The XML page provides instructions and a GoToURI action pointing to the
WebUI address.

## Path 2 — companion WebUI

The live menu is now an ASP.NET Core Minimal API server.

### Launch

```sh
dotnet run --project src/NexusCheatFramework.Web -- \
    --ip 192.168.1.50 --port 9080 --db ./cheats
```

### API endpoints

| Method | Path | Body | Returns |
|---|---|---|---|
| GET | `/api/state` | – | Structured JSON with connection, process, cheats, logs |
| GET | `/api/process` | – | `{ titleId, name, version, region, pid }` |
| GET | `/api/cheats` | – | Array of `{ id, name, description, enabled }` |
| POST | `/api/enable` | `{ "id": "cheat_id" }` | `{ success, cheatId, error }` |
| POST | `/api/disable` | `{ "id": "cheat_id" }` | `{ success, cheatId, error }` |
| POST | `/api/scan` | `{ "pattern": "...", "executableOnly": false, "maxResults": 1 }` | `{ matches, count }` |
| POST | `/api/reload-cheats` | – | `{ success }` |
| GET | `/api/config` | – | `{ consoleIp, consolePort, cheatDb, verbose }` |
| POST | `/api/config` | – | Stub; changes require restart |

All endpoints return structured JSON. Errors include a descriptive `error`
field. No raw exception dumps are returned.

### WebUI page

`menu/webui/index.html` features:
- Connection status badge (green/red)
- Detected process and Title ID
- Loaded cheat files and available cheats
- Enable/disable buttons per cheat
- AOB scan form (pattern input + results)
- Log/status area
- Error display

## Path 3 — CLI

```sh
dotnet run --project src/NexusCheatFramework.Cli -- info --ip 192.168.1.50 --db ./cheats
dotnet run --project src/NexusCheatFramework.Cli -- enable --ip 192.168.1.50 --db ./cheats --cheat infinite_health
dotnet run --project src/NexusCheatFramework.Cli -- disable --ip 192.168.1.50 --db ./cheats --cheat infinite_health
```

## Future

To get a one-press in-game overlay we'd need either:

- A Nexus payload endpoint that draws on-screen notifications, or
- A cooperating shellui hook (etaHEN territory; GPLv3) that calls
  `GoToURI("http://<host>:9080/")`.

Both are feasible but neither is implemented. The `ShortcutDetector` in
`Services/ShortcutDetector.cs` is already factored so it can drive either
path when a native pad polling endpoint is available.
